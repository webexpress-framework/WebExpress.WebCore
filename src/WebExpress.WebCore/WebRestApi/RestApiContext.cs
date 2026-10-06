using System;
using System.Collections.Generic;
using WebExpress.WebCore.WebApplication;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebCondition;
using WebExpress.WebCore.WebEndpoint;
using WebExpress.WebCore.WebIdentity;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebPlugin;
using WebExpress.WebCore.WebUri;

namespace WebExpress.WebCore.WebRestApi
{
    /// <summary>
    /// Read-only descriptor of a registered REST API endpoint, handed to components so they can
    /// learn about the API and the application and plugin it belongs to without referencing the
    /// endpoint instance itself.
    /// </summary>
    public class RestApiContext : IRestApiContext
    {
        /// <summary>
        /// Gets the associated plugin context.
        /// </summary>
        public IPluginContext PluginContext { get; internal set; }

        /// <summary>
        /// Gets the corresponding application context.
        /// </summary>
        public IApplicationContext ApplicationContext { get; internal set; }

        /// <summary>
        /// Gets the conditions that must be met for the resource to be active.
        /// </summary>
        public IEnumerable<ICondition> Conditions { get; internal set; } = [];

        /// <summary>
        /// Gets the crud methods.
        /// </summary>
        public IEnumerable<RequestMethod> Methods { get; internal set; } = [];

        /// <summary>
        /// Gets the endpoint id.
        /// </summary>
        public IComponentId EndpointId { get; internal set; }

        /// <summary>
        /// Gets the version number of the rest api.
        /// </summary>
        public uint Version { get; internal set; }

        /// <summary>
        /// Gets whether the resource is created once and reused each time it is called.
        /// </summary>
        public bool Cache { get; internal set; }

        /// <summary>
        /// Gets whether all subpaths should be taken into sitemap.
        /// </summary>
        public bool IncludeSubPaths { get; internal set; }

        /// <summary>
        /// Gets the attributes associated with the page.
        /// </summary>
        public IEnumerable<Attribute> Attributes { get; internal set; }

        /// <summary>
        /// Gets the collection of identity policies for the endpoint.
        /// </summary>
        public IEnumerable<IIdentityPolicy> Policies { get; internal set; }

        /// <summary>
        /// Gets the context path.
        /// </summary>
        public UriEndpoint ContextPath { get; internal set; }

        /// <summary>
        /// Gets the internal routing path for the endpoint.
        /// </summary>
        public IRoute Route { get; internal set; }

        /// <summary>
        /// Initializes a new instance of the class with the specified parent type and context path.
        /// </summary>
        public RestApiContext()
        {
        }

        /// <summary>
        /// Returns a string that represents the current object.
        /// </summary>
        /// <returns>A string that represents the current object.</returns>
        public override string ToString()
        {
            return $"RestApi: {EndpointId}";
        }
    }
}
