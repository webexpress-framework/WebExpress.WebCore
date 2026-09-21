using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebApplication;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebSession.Model;

namespace WebExpress.WebCore.WebSession
{
    /// <summary>
    /// Represents a session manager that handles session creation and retrieval.
    /// </summary>
    public class SessionManager : ISessionManager, ISystemComponent
    {
        /// <summary>
        /// The lifetime a session may sit idle before it is treated as gone, when nothing in the
        /// configuration overrides it. Bounded on purpose: an unbounded session (the previous
        /// year-long default, and a cookie that never expired) keeps a stolen id valid for as
        /// long as the attacker cares to hold it.
        /// </summary>
        public static readonly TimeSpan DefaultTimeout = TimeSpan.FromDays(30);

        private readonly IHttpServerContext _httpServerContext;
        private readonly SessionDictionary _dictionary = [];

        // guards every read of and write to _dictionary. A plain Dictionary is not safe for a
        // read concurrent with a write even on different keys, and RegenerateId's remove-then-add
        // must be atomic against a lookup, so a single lock - not a ConcurrentDictionary - is what
        // keeps the invariants
        private readonly object _sync = new();

        /// <summary>
        /// Gets or sets how long a session may stay idle before it expires. Applied as a sliding
        /// window - each access renews it - and used both to expire sessions on access and to
        /// bound the lifetime of the session cookie. A non-positive value disables expiry.
        /// </summary>
        public TimeSpan Timeout { get; set; } = DefaultTimeout;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="context">The reference to the context of the host.</param>
        [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Used via Reflection.")]
        private SessionManager(IHttpServerContext context)
        {
            _httpServerContext = context;

            _httpServerContext?.Log?.Debug
            (
                I18N.Translate("webexpress.webcore:sessionmanager.initialization")
            );
        }

        /// <summary>
        /// Returns the session a request belongs to, creating one when it has none yet.
        /// </summary>
        /// <remarks>
        /// The id in the session cookie is only ever a lookup key. An id the server has not
        /// issued - however well-formed - is never adopted: a client that could dictate its
        /// own id could plant that id in a victim's browser and read the victim's session once
        /// they signed in (session fixation). Every id is therefore generated here.
        ///
        /// An expired session is treated as absent - dropped and replaced with a fresh one -
        /// so a stale cookie never revives access, whether or not the monthly cleanup has run
        /// yet. Lookup, expiry and creation happen under one lock: a plain dictionary cannot be
        /// read and written concurrently, and splitting the check from the create would let two
        /// parallel requests for the same id each create a session.
        /// </remarks>
        /// <param name="request">The request.</param>
        /// <returns>The session.</returns>
        public Session GetSession(IRequest request)
        {
            // the session resolved for the request comes first: a session created for this
            // request has an id the client only learns with the response, so the cookie
            // cannot name it yet and a lookup by cookie would create a second one
            var existing = request is RequestBase concrete ? concrete.ExistingSession : request?.Session;
            if (existing is not null)
            {
                return existing;
            }

            var sessionCookie = request?.Header
                .Cookies?.FirstOrDefault(x => x.Name.Equals("session", StringComparison.OrdinalIgnoreCase));

            var hasId = Guid.TryParse(sessionCookie?.Value, out var id);
            var now = DateTime.Now;

            lock (_sync)
            {
                if (hasId && _dictionary.TryGetValue(id, out var known))
                {
                    if (!IsExpired(known, now))
                    {
                        // sliding window: an active session keeps renewing its idle deadline
                        known.Updated = now;
                        if (request is RequestBase knownRequest) { knownRequest.ExistingSession = known; }

                        return known;
                    }

                    // past its idle window: drop it so the id stops resolving, then fall
                    // through to hand out a fresh session
                    _dictionary.Remove(id);
                }

                // no, invalid, unknown or expired session id => a fresh, server-generated one
                var session = new Session();
                _dictionary[session.Id] = session;
                if (request is RequestBase newRequest) { newRequest.ExistingSession = session; }

                return session;
            }
        }

        /// <summary>
        /// Determines whether a session has sat idle past its timeout.
        /// </summary>
        /// <param name="session">The session to test.</param>
        /// <param name="now">The reference point for the idle span.</param>
        /// <returns>True when the session has expired; a non-positive timeout never expires.</returns>
        private bool IsExpired(Session session, DateTime now)
        {
            return Timeout > TimeSpan.Zero && now - session.Updated > Timeout;
        }

        /// <summary>
        /// Replaces the id of a session while keeping its state.
        /// </summary>
        /// <remarks>
        /// Called when a session gains privilege - at sign-in - so that an id the client held
        /// before, which an attacker may have planted or observed, no longer names the
        /// authenticated session. The old id stops resolving at once; the client learns the new
        /// one from the cookie sent with the response.
        /// </remarks>
        /// <param name="session">The session whose id is to be replaced.</param>
        /// <returns>The new session id.</returns>
        public Guid RegenerateId(Session session)
        {
            ArgumentNullException.ThrowIfNull(session);

            var id = Guid.NewGuid();

            lock (_sync)
            {
                _dictionary.Remove(session.Id);
                session.Id = id;
                _dictionary[id] = session;
                session.Updated = DateTime.Now;
            }

            return id;
        }

        /// <summary>
        /// Cleans up expired sessions from the session manager based on the specified 
        /// session timeout.
        /// </summary>
        /// <param name="applicationContext">
        /// The application context containing configuration settings, including the session 
        /// timeout duration.
        /// </param>
        /// <param name="timeoutMinutes">
        /// The explicit session timeout in minutes; if non-positive, the configured
        /// <see cref="Timeout"/> is used. If the effective timeout is non-positive, cleanup is
        /// skipped.
        /// </param>
        /// <returns>
        /// The current instance of the session manager, allowing for method chaining.
        /// </returns>
        /// <remarks>
        /// Expiry is already enforced on access, so this only reclaims the memory held by
        /// sessions no one has come back for. It shares <see cref="Timeout"/> with that access
        /// check by default, so the two never disagree on what "expired" means - the periodic
        /// sweep removes exactly what a lookup would already refuse.
        /// </remarks>
        public ISessionManager CleanUp(IApplicationContext applicationContext, int timeoutMinutes = 0)
        {
            // validate input
            ArgumentNullException.ThrowIfNull(applicationContext);

            // an explicit positive value wins; otherwise fall back to the configured sliding window
            var effectiveMinutes = timeoutMinutes > 0 ? timeoutMinutes : Timeout.TotalMinutes;

            // a non-positive effective timeout means "sessions never expire" => nothing to sweep
            if (effectiveMinutes <= 0)
            {
                return this;
            }

            var now = DateTime.Now;

            // collect expired ids under lock to avoid concurrent modifications during enumeration.
            // the query must be materialized (ToList) before removing - otherwise the deferred
            // enumeration would mutate _dictionary.Values while iterating it (InvalidOperationException)
            // and the subsequent logging loop would re-evaluate to an empty result.
            List<Guid> expiredIds;
            lock (_sync)
            {
                expiredIds = _dictionary.Values
                    .Where(s => (now - s.Updated).TotalMinutes > effectiveMinutes)
                    .Select(s => s.Id)
                    .ToList();

                // remove expired sessions under the same lock
                foreach (var id in expiredIds)
                {
                    _dictionary.Remove(id);
                }
            }

            // log removals outside the lock
            foreach (var id in expiredIds)
            {
                _httpServerContext?.Log?.Info
                (
                    I18N.Translate("webexpress.webcore:sessionmanager.cleanup.removed", id)
                );
            }

            return this;
        }

        /// <summary>
        /// Release of unmanaged resources reserved during use.
        /// </summary>
        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }
}
