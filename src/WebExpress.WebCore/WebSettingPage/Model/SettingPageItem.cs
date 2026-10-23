using System;
using System.Collections.Generic;
using WebExpress.WebCore.WebApplication;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebCondition;
using WebExpress.WebCore.WebEndpoint;
using WebExpress.WebCore.WebIcon;
using WebExpress.WebCore.WebPlugin;
using WebExpress.WebCore.WebUri;

namespace WebExpress.WebCore.WebSettingPage.Model
{
    /// <summary>
    /// Internal record the settings system keeps for one registered setting page, linking the page
    /// to the group it appears in and to the application and plugin that provided it.
    /// </summary>
    public class SettingPageItem : IDisposable
    {
        /// <summary>
        /// Gets the endpoint id.
        /// </summary>
        public IComponentId EndpointId { get; internal set; }

        /// <summary>
        /// Gets the context of the associated plugin.
        /// </summary>
        public IPluginContext PluginContext { get; internal set; }

        /// <summary>
        /// Gets the application context.
        /// </summary>
        public IApplicationContext ApplicationContext { get; internal set; }

        /// <summary>
        /// Gets the setting page context.
        /// </summary>
        public ISettingPageContext SettingPageContext { get; internal set; }

        /// <summary>
        /// Gets the class type of the setting page.
        /// </summary>
        public Type SettingPageClass { get; internal set; }

        /// <summary>
        /// Gets the instance of the setting page, if the page is cached, otherwise null.
        /// </summary>
        public IEndpoint Instance { get; internal set; }

        /// <summary>
        /// Gets or sets the parent type.
        /// </summary>
        public Type ParentType { get; set; }

        /// <summary>
        /// Gets or sets the paths of the resource.
        /// </summary>
        public UriEndpoint ContextPath { get; set; }

        /// <summary>
        /// Gets the path segment.
        /// </summary>
        public IUriPathSegment PathSegment { get; internal set; }

        /// <summary>
        /// Gets the group type.
        /// </summary>
        public Type SettingGroupType { get; internal set; }

        /// <summary>
        /// Gets the section.
        /// </summary>
        public SettingSection Section { get; internal set; }

        /// <summary>
        /// Gets a value indicating whether the component is created once and reused on each execution.
        /// </summary>
        public bool Cache { get; internal set; }

        /// <summary>
        /// Gets the icon.
        /// </summary>
        public IIcon Icon { get; internal set; }

        /// <summary>
        /// Gets the setting page title.
        /// </summary>
        public string PageTitle { get; internal set; }

        /// <summary>
        /// Gets a value indicating whether the page should be displayed or hidden.
        /// </summary>
        public bool Hide { get; internal set; }

        /// <summary>
        /// Gets whether all subpaths should be taken into sitemap.
        /// </summary>
        public bool IncludeSubPaths { get; internal set; }

        /// <summary>
        /// Gets the attributes associated with the page.
        /// </summary>
        public IEnumerable<Type> Attributes { get; internal set; }

        /// <summary>
        /// Gets the scope names that provides the resource. The scope name
        /// is a string with a name (e.g. global, admin), which can be used by elements to 
        /// determine whether content and how content should be displayed.
        /// </summary>
        public IEnumerable<Type> Scopes { get; internal set; } = [];

        /// <summary>
        /// Gets the conditions that must be met for the resource to be active.
        /// </summary>
        public IEnumerable<ICondition> Conditions { get; internal set; } = [];

        /// <summary>
        /// Gets the group context to which the setting page belongs.
        /// </summary>
        public ISettingGroupContext SettingGroup { get; internal set; }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="endpointManager">The endpoint manager responsible for managing endpoints.</param>
        internal SettingPageItem(IEndpointManager endpointManager)
        {
        }

        /// <summary>
        /// Release of unmanaged resources reserved during use.
        /// </summary>
        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }
}
