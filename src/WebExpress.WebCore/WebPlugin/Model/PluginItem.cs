using System;
using System.Collections.Generic;
using System.Threading;

namespace WebExpress.WebCore.WebPlugin.Model
{
    /// <summary>
    /// Internal record the plugin manager keeps for one loaded plugin. It bundles everything needed
    /// to run and later unload the plugin: its load context, plugin class and instance, public
    /// context, declared dependencies, and the application types it supports.
    /// </summary>
    internal class PluginItem
    {
        /// <summary>
        /// The plugin load context for isolating and unloading the dependent libraries.
        /// </summary>
        public PluginLoadContext PluginLoadContext { get; internal set; }

        /// <summary>
        /// Gets the plugin class.
        /// </summary>
        public Type PluginClass { get; internal set; }

        /// <summary>
        /// Gets the context associated with the plugin.
        /// </summary>
        public IPluginContext PluginContext { get; internal set; }

        /// <summary>
        /// Gets the plugin.
        /// </summary>
        public IPlugin Plugin { get; internal set; }

        /// <summary>
        /// Gets the dependencies of the plugin.
        /// </summary>
        public IEnumerable<string> Dependencies { get; internal set; } = [];

        /// <summary>
        /// Gets the types of applications that the plugin supports.
        /// </summary>
        public IEnumerable<Type> ApplicationTypes { get; internal set; } = [];

        /// <summary>
        /// Gets or sets a value indicating whether the plugin was asked to boot while it still
        /// waited for its dependencies. Such a plugin is booted as soon as they are fulfilled;
        /// otherwise nobody would ever boot it, since its package was booted already.
        /// </summary>
        public bool BootRequested { get; internal set; }

        /// <summary>
        /// Gets the thread termination token.
        /// </summary>
        public CancellationTokenSource CancellationTokenSource { get; } = new CancellationTokenSource();
    }
}
