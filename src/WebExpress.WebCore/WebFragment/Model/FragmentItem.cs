using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
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
        // one compiled call per fragment class, shared by all items and requests. It takes the
        // instance as its first argument: a delegate that captured the instance of the first
        // request rendered every later request on that foreign instance - its state leaked
        // across requests and users, and concurrent requests rendered on it at the same time.
        private static readonly ConcurrentDictionary<Type, Func<object, object, object, IHtmlNode>> _delegateCache = new();

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
            if (Cache && _instance is not null)
            {
                return (TFragment)_instance;
            }

            var instance = ComponentActivator.CreateInstance<IFragmentBase, IFragmentContext>
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
                // two requests may create an instance at once; both use the one that is kept
                instance = Interlocked.CompareExchange(ref _instance, instance, null) ?? instance;
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
                var instance = CreateInstance<IFragmentBase>(renderContext?.PageContext);
                var render = _delegateCache.GetOrAdd(FragmentClass, CompileRender);

                return render(instance, renderContext, visualTree);
            }

            return null;
        }

        /// <summary>
        /// Compiles the call of the fragment's render method with the instance, the render context
        /// and the visual tree as arguments.
        /// </summary>
        /// <param name="fragmentClass">The fragment class.</param>
        /// <returns>The compiled call.</returns>
        private static Func<object, object, object, IHtmlNode> CompileRender(Type fragmentClass)
        {
            var arguments = fragmentClass.GetInterface(typeof(IFragment<,>).Name).GetGenericArguments();
            var renderMethod = fragmentClass.GetMethod("Render", [arguments[0], arguments[1]]);
            var instanceParam = Expression.Parameter(typeof(object), "instance");
            var renderContextParam = Expression.Parameter(typeof(object), "renderContext");
            var visualTreeParam = Expression.Parameter(typeof(object), "visualTree");
            var call = Expression.Call
            (
                Expression.Convert(instanceParam, fragmentClass),
                renderMethod,
                Expression.Convert(renderContextParam, arguments[0]),
                Expression.Convert(visualTreeParam, arguments[1])
            );

            return Expression.Lambda<Func<object, object, object, IHtmlNode>>
            (
                Expression.TypeAs(call, typeof(IHtmlNode)),
                instanceParam,
                renderContextParam,
                visualTreeParam
            ).Compile();
        }

        /// <summary>
        /// Releases the instance kept for a cached fragment, so a removed plugin leaves none behind.
        /// </summary>
        public void Dispose()
        {
            (Interlocked.Exchange(ref _instance, null) as IDisposable)?.Dispose();
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
