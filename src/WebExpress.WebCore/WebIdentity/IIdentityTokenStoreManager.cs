using System.Collections.Generic;
using WebExpress.WebCore.WebApplication;
using WebExpress.WebCore.WebComponent;

namespace WebExpress.WebCore.WebIdentity
{
    /// <summary>
    /// Resolves the single durable store that holds replay and revocation markers for an application.
    /// Unlike identity providers, an application never uses two stores at once, because a revocation
    /// recorded in one store would be invisible to a lookup in the other.
    /// </summary>
    public interface IIdentityTokenStoreManager : IComponentManager
    {
        /// <summary>
        /// Returns every store currently in use, for diagnostics and administration only.
        /// Request handling must resolve its store through <see cref="GetStore"/> instead.
        /// </summary>
        IEnumerable<IIdentityTokenStore> Stores { get; }

        /// <summary>
        /// Returns the store bound to the application, falling back to the shared file store of the deployment.
        /// </summary>
        /// <param name="applicationContext">The application whose credentials are consumed or revoked.</param>
        /// <returns>The store responsible for the application, or null when no durable storage is configured.</returns>
        IIdentityTokenStore GetStore(IApplicationContext applicationContext);

        /// <summary>
        /// Replaces the application's current binding so plugins can supply distributed or database-backed storage.
        /// </summary>
        /// <param name="store">The store that receives the application's replay and revocation markers.</param>
        /// <param name="applicationContext">The application whose credentials are consumed or revoked.</param>
        void Register(IIdentityTokenStore store, IApplicationContext applicationContext);

        /// <summary>
        /// Removes an explicit binding so the application returns to the default file store.
        /// </summary>
        /// <param name="store">The store that receives the application's replay and revocation markers.</param>
        /// <param name="applicationContext">The application whose credentials are consumed or revoked.</param>
        /// <returns>True when the binding existed and was removed; otherwise, false.</returns>
        bool Unregister(IIdentityTokenStore store, IApplicationContext applicationContext);
    }
}
