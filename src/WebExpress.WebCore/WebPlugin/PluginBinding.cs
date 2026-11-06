using System.Collections.Generic;
using System.Linq;
using WebExpress.WebCore.WebApplication;
using WebExpress.WebCore.WebComponent;

namespace WebExpress.WebCore.WebPlugin
{
    /// <summary>
    /// Decides which applications a plugin's endpoints are bound to when the plugin arrives.
    /// A plugin serves the applications defined in its own assembly and those it names through
    /// an application attribute, so a plugin can extend an application shipped by another plugin.
    /// </summary>
    internal static class PluginBinding
    {
        /// <summary>
        /// Returns the applications a newly added plugin must be bound to. Applications that are
        /// registered after the plugin are bound later through the application event; the managers'
        /// containment checks keep that second path from binding a pair twice.
        /// </summary>
        /// <param name="componentHub">The component hub that knows the applications and plugins.</param>
        /// <param name="pluginContext">The plugin whose endpoints are bound.</param>
        /// <returns>Every application of the plugin, each one once.</returns>
        public static IEnumerable<IApplicationContext> GetBoundApplications(this IComponentHub componentHub, IPluginContext pluginContext)
        {
            var own = componentHub?.ApplicationManager?.GetApplications(pluginContext) ?? [];
            var associated = componentHub?.PluginManager?.GetAssociatedApplications(pluginContext) ?? [];

            return [.. own.Concat(associated).Where(x => x is not null).Distinct()];
        }
    }
}
