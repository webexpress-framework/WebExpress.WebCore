using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.Json.Nodes;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebApplication;
using WebExpress.WebCore.WebCluster;
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

        /// <summary>
        /// The scope sessions are kept under in the cluster store.
        /// </summary>
        internal const string StoreScope = "session";

        /// <summary>
        /// How often a session that is only read gets its idle deadline renewed in the cluster
        /// store. Renewing on every request would turn every page view into a write to shared
        /// storage; a minute of drift is nothing against an idle window of hours or days.
        /// </summary>
        internal static readonly TimeSpan RenewInterval = TimeSpan.FromMinutes(1);

        // a disabled timeout still needs a bound in a shared store, or abandoned sessions would
        // accumulate there forever
        private static readonly TimeSpan UnboundedLifetime = TimeSpan.FromDays(365);

        // a commit holds the lock of its session for a read and a write; the lifetime only matters
        // for an instance that dies in between, the timeout for one that is slow
        private static readonly TimeSpan CommitLockLifetime = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan CommitLockTimeout = TimeSpan.FromSeconds(2);

        // every unserializable property type is reported once, not on every request
        private static readonly ConcurrentDictionary<Type, bool> _unserializable = new();

        private readonly IHttpServerContext _httpServerContext;
        private readonly IComponentHub _componentHub;
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
        /// Gets the number of sessions held, including expired ones the cleanup has not removed yet.
        /// </summary>
        public int Count
        {
            get
            {
                if (SharedStore is { } store)
                {
                    return store.Count(StoreScope);
                }

                lock (_sync)
                {
                    return _dictionary.Count;
                }
            }
        }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="componentHub">The component hub providing the cluster store.</param>
        /// <param name="context">The reference to the context of the host.</param>
        [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Used via Reflection.")]
        private SessionManager(IComponentHub componentHub, IHttpServerContext context)
        {
            _httpServerContext = context;
            _componentHub = componentHub;

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

            if (SharedStore is { } store)
            {
                // the store only knows ids this cluster issued, so fixation stays impossible
                var shared = (hasId ? Load(store, id) : null) is { } loaded && !IsExpired(loaded, now)
                    ? loaded
                    : new Session();

                shared.Updated = now;
                if (request is RequestBase sharedRequest) { sharedRequest.ExistingSession = shared; }

                return shared;
            }

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

            if (SharedStore is { } store)
            {
                store.Remove(StoreScope, session.Id.ToString());
                session.Id = id;
                session.Updated = DateTime.Now;

                // written right away: the old id is gone, and a parallel request carrying the new
                // cookie must find the session before this request has finished
                session.PersistedFingerprint = null;
                Commit(session);

                return id;
            }

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
            // a shared store expires its entries itself, for every instance at once
            if (effectiveMinutes <= 0 || SharedStore is not null)
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
        /// Writes a session to the cluster store when its properties changed, or when its idle
        /// deadline is due for renewal.
        /// </summary>
        /// <param name="session">The session.</param>
        public void Commit(Session session)
        {
            if (session is null || SharedStore is not { } store)
            {
                return;
            }

            var ours = SessionSerializer.Serialize(session, ReportUnserializable);
            var now = DateTime.Now;

            if (session.PersistedFingerprint is { } persisted
                && persisted.AsSpan().SequenceEqual(ours.Fingerprint)
                && now - session.PersistedAt < RenewInterval)
            {
                return;
            }

            var key = session.Id.ToString();

            try
            {
                // read, merge and write must not interleave with another instance committing the
                // same session; a lock not obtained in time still merges, with a small window left
                using var guard = _componentHub?.ClusterManager?.Lock(StoreScope + "/" + key, CommitLockLifetime, CommitLockTimeout);

                var properties = ours.Properties;

                // another request of the same session may have written it on another instance
                // since this one read it; its changes are merged in instead of overwritten
                if (store.Get(StoreScope, key) is { } stored
                    && SessionSerializer.ReadProperties(stored) is { } theirs
                    && !JsonNode.DeepEquals(theirs, session.LoadedProperties))
                {
                    properties = SessionMerge.Merge(session.LoadedProperties, ours.Properties, theirs) as JsonObject ?? ours.Properties;
                }

                store.Set(StoreScope, key, SessionSerializer.Compose(session, properties), Timeout > TimeSpan.Zero ? Timeout : UnboundedLifetime);
                session.PersistedFingerprint = ours.Fingerprint;
                session.LoadedProperties = ours.Properties;
                session.PersistedAt = now;
            }
            catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException)
            {
                // the response is already decided; the session keeps its previously stored state
                _httpServerContext?.Log?.Exception(ex);
            }
        }

        /// <summary>
        /// Returns the cluster store when other instances share it, or null while sessions stay
        /// in this process.
        /// </summary>
        private IClusterStore SharedStore => _componentHub?.ClusterManager?.Store is { IsShared: true } store ? store : null;

        /// <summary>
        /// Reads a session from the cluster store.
        /// </summary>
        /// <param name="store">The cluster store.</param>
        /// <param name="id">The session id from the cookie.</param>
        /// <returns>The session, or null when the store does not know the id.</returns>
        private Session Load(IClusterStore store, Guid id)
        {
            var content = store.Get(StoreScope, id.ToString());

            if (content is null)
            {
                return null;
            }

            var loaded = SessionSerializer.Deserialize(content, type => _httpServerContext?.Log?.Debug
            (
                I18N.Translate("webexpress.webcore:sessionmanager.cluster.dropped", type, id)
            ));

            // the stored id must be the looked-up one; anything else is not this session
            if (loaded is not { } result || result.Session.Id != id)
            {
                return null;
            }

            // the base of a later merge is what this instance could recreate: a property dropped
            // here is absent from base and change alike, so it survives for the instances knowing it
            var session = result.Session;
            var own = SessionSerializer.Serialize(session);

            session.PersistedFingerprint = own.Fingerprint;
            session.LoadedProperties = own.Properties;
            session.PersistedAt = session.Updated;

            return session;
        }

        /// <summary>
        /// Reports a session property that cannot travel to the other instances. It still works
        /// on this instance, which is why it is a warning and not an error.
        /// </summary>
        /// <param name="type">The property type.</param>
        /// <param name="ex">The cause.</param>
        private void ReportUnserializable(Type type, Exception ex)
        {
            if (_unserializable.TryAdd(type, true))
            {
                _httpServerContext?.Log?.Warning
                (
                    I18N.Translate("webexpress.webcore:sessionmanager.cluster.unserializable", type.FullName, ex.Message)
                );
            }
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
