using System.Collections.Generic;
using WebExpress.WebCore.WebApplication;
using WebExpress.WebCore.WebComponent;

namespace WebExpress.WebCore.WebIdentity
{
    /// <summary>
    /// Keeps authentication sources scoped to the application and plugin that own them.
    /// </summary>
    public interface IIdentityProviderManager : IComponentManager
    {
        /// <summary>
        /// Returns a snapshot so a request can safely enumerate providers during plugin changes.
        /// </summary>
        /// <param name="applicationContext">The application context that owns the requested authentication operation.</param>
        /// <returns>A stable snapshot of the application's registered authentication sources.</returns>
        IEnumerable<IIdentityProvider> GetProviders(IApplicationContext applicationContext);
        /// <summary>
        /// Allows application-specific provider configuration in addition to plugin discovery.
        /// </summary>
        /// <param name="provider">The authentication source being associated with an application.</param>
        /// <param name="applicationContext">The application context that owns the requested authentication operation.</param>
        void Register(IIdentityProvider provider, IApplicationContext applicationContext);
        /// <summary>
        /// Removes an explicitly registered source from subsequent authentication attempts.
        /// </summary>
        /// <param name="provider">The authentication source being associated with an application.</param>
        /// <param name="applicationContext">The application context that owns the requested authentication operation.</param>
        /// <returns>True when a provider binding was removed; otherwise, false.</returns>
        bool Unregister(IIdentityProvider provider, IApplicationContext applicationContext);
    }
}
