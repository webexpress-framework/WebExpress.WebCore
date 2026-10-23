using System;
using System.Collections.Generic;
using WebExpress.WebCore.WebApplication;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebCondition;
using WebExpress.WebCore.WebEndpoint;
using WebExpress.WebCore.WebIcon;
using WebExpress.WebCore.WebIdentity;
using WebExpress.WebCore.WebPlugin;
using WebExpress.WebCore.WebUri;

namespace WebExpress.WebCore.WebPage
{
    /// <summary>
    /// Read-only descriptor of a registered page, handed to components so they can learn about the
    /// page and the application and plugin it belongs to without referencing the page instance itself.
    /// </summary>
    public class PageContext : IPageContext
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
        /// Gets the scope names that provides the resource. The scope name
        /// is a string with a name (e.g. global, admin), which can be used by elements to 
        /// determine whether content and how content should be displayed.
        /// </summary>
        public IEnumerable<Type> Scopes { get; internal set; } = [];

        /// <summary>
        /// Gets the collection of domain types associated with the decorated element.
        /// Domains represent logical application areas such as workspaces, modules
        /// or functional segments and can be used for routing, filtering or contextual grouping.
        /// </summary>
        public IEnumerable<Type> Domains { get; internal set; } = [];

        /// <summary>
        /// Gets the conditions that must be met for the resource to be active.
        /// </summary>
        public IEnumerable<ICondition> Conditions { get; internal set; } = [];

        /// <summary>
        /// Gets the endpoint id.
        /// </summary>
        public IComponentId EndpointId { get; internal set; }

        /// <summary>
        /// Gets the page title.
        /// </summary>
        public string PageTitle { get; internal set; }

        /// <summary>
        /// Gets the page icon.
        /// </summary>
        public IIcon PageIcon { get; internal set; }

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
        /// Returns the internal routing path for the endpoint.
        /// </summary>
        public IRoute Route { get; internal set; }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public PageContext()
        {
        }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="endpointContext">The endpoint context.</param>
        /// <param name="title">The page title.</param>
        /// <param name="icon">The page icon.</param>
        /// <param name="scopes">The scope types.</param>
        /// <param name="domains">The domain types.</param>
        public PageContext(WebEndpoint.IEndpointContext endpointContext, string title = null, IIcon icon = null, IEnumerable<Type> scopes = null, IEnumerable<Type> domains = null)
        {
            PluginContext = endpointContext.PluginContext;
            ApplicationContext = endpointContext.ApplicationContext;
            Conditions = endpointContext.Conditions;
            EndpointId = endpointContext.EndpointId;
            Cache = endpointContext.Cache;
            IncludeSubPaths = endpointContext.IncludeSubPaths;
            Attributes = endpointContext.Attributes;
            Policies = endpointContext.Policies;
            Route = endpointContext.Route;
            PageTitle = title;
            PageIcon = icon;
            Scopes = scopes;
            Domains = domains;
        }

        /// <summary>
        /// Returns a string that represents the current object.
        /// </summary>
        /// <returns>A string that represents the current object.</returns>
        public override string ToString()
        {
            return $"Page: {EndpointId}";
        }
    }
}
