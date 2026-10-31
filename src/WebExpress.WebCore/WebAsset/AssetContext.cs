using System;
using System.Collections.Generic;
using WebExpress.WebCore.WebApplication;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebCondition;
using WebExpress.WebCore.WebEndpoint;
using WebExpress.WebCore.WebIdentity;
using WebExpress.WebCore.WebPlugin;

namespace WebExpress.WebCore.WebAsset
{
    /// <summary>
    /// Read-only descriptor of a registered asset endpoint, handed to components so they can learn
    /// about the asset and the application and plugin it belongs to without referencing the asset itself.
    /// </summary>
    public class AssetContext : IAssetContext
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
        public IEnumerable<ICondition> Conditions => [];

        /// <summary>
        /// Gets the resource id.
        /// </summary>
        public IComponentId EndpointId { get; internal set; }

        /// <summary>
        /// Gets whether the resource is created once and reused each time it is called.
        /// </summary>
        public bool Cache => true;

        /// <summary>
        /// Gets whether all subpaths should be taken into sitemap.
        /// </summary>
        public bool IncludeSubPaths { get; internal set; }

        /// <summary>
        /// Gets the internal routing path for the endpoint.
        /// </summary>
        public IRoute Route { get; internal set; }

        /// <summary>
        /// Gets the attributes associated with the page.
        /// </summary>
        public IEnumerable<Attribute> Attributes => [];

        /// <summary>
        /// Gets the collection of identity policies for the endpoint.
        /// </summary>
        public IEnumerable<IIdentityPolicy> Policies => [];

        /// <summary>
        /// Initializes a new instance of the class with the specified endpoint manager, parent type, context path, and path segment.
        /// </summary>
        public AssetContext()
        {
        }

        /// <summary>
        /// Returns a string that represents the current object.
        /// </summary>
        /// <returns>A string that represents the current object.</returns>
        public override string ToString()
        {
            return $"Asset: {EndpointId}";
        }
    }
}
