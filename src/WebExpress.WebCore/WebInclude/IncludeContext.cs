using System;
using System.Collections.Generic;
using WebExpress.WebCore.WebApplication;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebPlugin;

namespace WebExpress.WebCore.WebInclude
{
    /// <summary>
    /// Default include context implementation.
    /// </summary>
    internal sealed class IncludeContext : IIncludeContext
    {
        /// <summary>
        /// Gets or sets the identifier of the included component.
        /// </summary>
        public ComponentId IncludeId { get; set; }

        /// <summary>
        /// Gets or sets the context for the plugin, providing access to shared resources and services.
        /// </summary>
        public IPluginContext PluginContext { get; set; }

        /// <summary>
        /// Gets or sets the application context that provides configuration and services for the application.
        /// </summary>
        public IApplicationContext ApplicationContext { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether caching is enabled.
        /// </summary>
        public bool Cache { get; set; }

        /// <summary>
        /// Gets or sets the collection of files to be included.
        /// </summary>
        public IEnumerable<IncludeFile> Files { get; set; } = [];

        /// <summary>
        /// Gets the files a page links for the include, in the order they are to be linked.
        /// </summary>
        public IEnumerable<IncludeFile> LinkedFiles { get; set; } = [];

        /// <summary>
        /// Gets or sets the collection of scopes associated with the current context.
        /// </summary>
        /// <remarks>
        /// The collection can be empty if no scopes are defined. Callers can set this property
        /// to customize the applicable scopes or retrieve it to inspect the current configuration.
        /// </remarks>
        public IEnumerable<Type> Scopes { get; set; } = [];
    }
}
