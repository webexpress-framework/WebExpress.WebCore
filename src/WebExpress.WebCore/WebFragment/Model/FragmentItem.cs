using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using WebExpress.WebCore.WebApplication;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebCondition;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebPage;
using WebExpress.WebCore.WebPlugin;

namespace WebExpress.WebCore.WebFragment.Model
{
    /// <summary>
    /// Fragments are components that can be integrated into pages to dynamically expand functionalities.
    /// </summary>
    internal class FragmentItem : IDisposable
    {
        private IFragmentBase _instance;
        private readonly IComponentHub _componentHub;
        private readonly IHttpServerContext _httpServerContext;
        private static readonly Dictionary<Type, Delegate> _delegateCache = [];

        /// <summary>
        /// Gets or sets the context of the associated plugin.
        /// </summary>
        public IPluginContext PluginContext { get; set; }

        /// <summary>
        /// Gets or sets the application context.
        /// </summary>
        public IApplicationContext ApplicationContext { get; set; }

        /// <summary>
        /// Gets or sets the fragment context.
        /// </summary>
        public IFragmentContext FragmentContext { get; set; }

        /// <summary>
        /// Gets or sets the type of fragment.
        /// </summary>
        public Type FragmentClass { get; set; }

        /// <summary>
        /// Gets or sets the section.
        /// </summary>
        public Type Section { get; set; }

        /// <summary>
        /// Gets or sets the scope.
        /// </summary>
        public Type Scope { get; set; }

        /// <summary>
        /// Gets or sets the conditions that must be met for the component to be active.
        /// </summary>
        public ICollection<ICondition> Conditions { get; set; }

        /// <summary>
        /// Gets or sets the order of the fragment.
        /// </summary>
        public int Order { get; set; }

        /// <summary>
        /// Determines whether the component is created once and reused on each execution.
        /// </summary>
        public bool Cache { get; set; }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="componentHub">The component hub.</param>
        /// <param name="httpServerContext">The context of the HTTP server.</param>
        public FragmentItem(IComponentHub componentHub, IHttpServerContext httpServerContext)
        {
            _componentHub = componentHub;
            _httpServerContext = httpServerContext;
        }

        /// <summary>
        /// Create the instance of the component.
        /// </summary>
        /// <param name="pageContext">The page context.</param>
        public TFragment CreateInstance<TFragment>(IPageContext pageContext = null)
            where TFragment : IFragmentBase
        {
            var instance = _instance;

            instance ??= ComponentActivator.CreateInstance<IFragmentBase, IFragmentContext>
            (
                FragmentClass,
                FragmentContext,
                _httpServerContext,
                _componentHub,
                FragmentContext,
                pageContext
            );

            if (Cache)
            {
                _instance = instance;
            }

            return (TFragment)instance;
        }

        /// <summary>
        /// Processes the fragments for a given section within the specified render context.
        /// </summary>
        /// <typeparam name="TRenderContext">The type of the render context.</typeparam>
        /// <typeparam name="TVisualTree">The type of the visual tree.</typeparam>
        /// <param name="renderContext">The context in which rendering occurs.</param>
        /// <param name="visualTree">The visual tree to be rendered.</param>
        /// <returns>An HTML node representing the rendered fragments. Can be null if no nodes are present.</returns>
        public IHtmlNode Render<TRenderContext, TVisualTree>(TRenderContext renderContext, TVisualTree visualTree)
            where TRenderContext : IRenderContext
            where TVisualTree : IVisualTree
        {
            if (FragmentContext.Check(renderContext?.Request))
            {
                var instance = CreateInstance<IFragmentBase>();

                if (!_delegateCache.TryGetValue(FragmentClass, out var del))
                {
                    // create and compile the expression
                    var renderContextType = FragmentClass.GetInterface(typeof(IFragment<,>).Name).GetGenericArguments()[0];
                    var visualTreeType = FragmentClass.GetInterface(typeof(IFragment<,>).Name).GetGenericArguments()[1];
                    var renderContextParam = Expression.Parameter(renderContextType, "renderContext");
                    var visualTreeParam = Expression.Parameter(visualTreeType, "visualTree");
                    var renderMethod = FragmentClass.GetMethod("Render", [renderContextType, visualTreeType]);
                    var callProzessMethod = Expression.Call
                    (
                        Expression.Constant(instance),
                        renderMethod,
                        renderContextParam,
                        visualTreeParam
                    );
                    var lambda = Expression.Lambda(callProzessMethod, renderContextParam, visualTreeParam)
                        .Compile();

                    _delegateCache[FragmentClass] = lambda;
                    del = lambda;
                }

                // execute the cached delegate
                var html = del.DynamicInvoke(renderContext, visualTree) as IHtmlNode;

                return html;
            }

            return null;
        }

        /// <summary>
        /// Performs application-specific tasks related to sharing, returning, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
        }

        /// <summary>
        /// Convert the resource element to a string.
        /// </summary>
        /// <returns>The resource element in its string representation.</returns>
        public override string ToString()
        {
            return $"Fragment: '{FragmentContext.FragmentId}'";
        }
    }
}
