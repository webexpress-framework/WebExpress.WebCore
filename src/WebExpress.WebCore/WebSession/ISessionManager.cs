using System;
using WebExpress.WebCore.WebApplication;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebSession.Model;

namespace WebExpress.WebCore.WebSession
{
    /// <summary>
    /// Represents a session manager that handles session creation and retrieval.
    /// </summary>
    public interface ISessionManager : IComponentManager
    {
        /// <summary>
        /// Gets or sets how long a session may stay idle before it expires. Applied as a sliding
        /// window and used both to expire sessions on access and to bound the session cookie's
        /// lifetime. A non-positive value disables expiry.
        /// </summary>
        TimeSpan Timeout { get; set; }

        /// <summary>
        /// Gets the number of sessions held, including expired ones the cleanup has not removed yet.
        /// </summary>
        int Count { get; }

        /// <summary>
        /// Returns the session a request belongs to, creating one when it has none yet.
        /// </summary>
        /// <remarks>
        /// The id in the session cookie is only ever a lookup key; an id the server has not
        /// issued is never adopted, so a client cannot fix the id of a session in advance.
        /// </remarks>
        /// <param name="request">The request.</param>
        /// <returns>The session.</returns>
        Session GetSession(IRequest request);

        /// <summary>
        /// Replaces the id of a session while keeping its state.
        /// </summary>
        /// <remarks>
        /// Meant for the moment a session gains privilege - at sign-in - so that an id the client
        /// held before no longer names the authenticated session. The client learns the new id
        /// from the cookie sent with the response.
        /// </remarks>
        /// <param name="session">The session whose id is to be replaced.</param>
        /// <returns>The new session id.</returns>
        Guid RegenerateId(Session session);

        /// <summary>
        /// Writes a session back once the request that used it is answered. A single instance
        /// keeps sessions in memory and writes nothing; in a cluster the session is stored where
        /// every instance finds it, so the next request may land on any of them.
        /// </summary>
        /// <param name="session">The session.</param>
        void Commit(Session session);

        /// <summary>
        /// Cleans up expired sessions from the session manager based on the specified session timeout.
        /// </summary>
        /// <remarks>
        /// This method iterates through the sessions and removes those that have been inactive
        /// for longer than the configured session timeout. It logs the removal of each expired session.
        /// </remarks>
        /// <param name="applicationContext">
        /// The application context containing configuration settings, including the session timeout duration.
        /// </param>
        /// <param name="timeoutMinutes">
        /// The explicit session timeout in minutes; if non-positive, the configured timeout is used. If 
        /// the effective timeout is non-positive, cleanup is skipped.
        /// </param>
        /// <returns>The current instance of the session manager, allowing for method chaining.</returns>
        ISessionManager CleanUp(IApplicationContext applicationContext, int timeoutMinutes = 0);
    }
}
