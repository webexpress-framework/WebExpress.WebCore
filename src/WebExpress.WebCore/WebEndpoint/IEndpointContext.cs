using System;
using System.Collections.Generic;
using WebExpress.WebCore.WebApplication;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebCondition;
using WebExpress.WebCore.WebIdentity;
using WebExpress.WebCore.WebPlugin;

namespace WebExpress.WebCore.WebEndpoint
{
    /// <summary>
    /// Read-only descriptor of a registered endpoint (a page, resource, REST API, …). It tells the
    /// framework and other components what they need to know about the endpoint — its id, the
    /// application and plugin it belongs to, and its routing details — without referencing the
    /// endpoint instance itself.
    /// </summary>
    public interface IEndpointContext : IContext
    {
        /// <summary>
        /// Gets the endpoint id.
        /// </summary>
        IComponentId EndpointId { get; }

        /// <summary>
        /// Gets the associated plugin context.
        /// </summary>
        IPluginContext PluginContext { get; }

        /// <summary>
        /// Gets the corresponding application context.
        /// </summary>
        IApplicationContext ApplicationContext { get; }

        /// <summary>
        /// Gets the conditions that must be met for the resource to be active.
        /// </summary>
        IEnumerable<ICondition> Conditions { get; }

        /// <summary>
        /// Determines whether the resource is created once and reused each time it is called.
        /// </summary>
        bool Cache { get; }

        /// <summary>
        /// Gets whether all subpaths should be taken into sitemap.
        /// </summary>
        bool IncludeSubPaths { get; }

        /// <summary>
        /// Gets the internal routing path for the endpoint.
        /// </summary>
        IRoute Route { get; }

        /// <summary>
        /// Gets the attributes associated with the endpoint.
        /// </summary>
        IEnumerable<Attribute> Attributes { get; }

        /// <summary>
        /// Gets the collection of identity policies for the endpoint.
        /// </summary>
        IEnumerable<IIdentityPolicy> Policies { get; }
    }
}
