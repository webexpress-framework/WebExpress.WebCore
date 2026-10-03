using System;
using System.Collections.Generic;
using WebExpress.WebCore.WebApplication;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebPage;
using WebExpress.WebCore.WebScope;
using WebExpress.WebCore.WebSection;

namespace WebExpress.WebCore.WebFragment
{
    /// <summary>
    /// Interface for managing web fragments.
    /// </summary>
    public interface IFragmentManager : IComponentManager
    {
        /// <summary>
        /// An event that fires when a fragment is added.
        /// </summary>
        event EventHandler<IFragmentContext> AddFragment;

        /// <summary>
        /// An event that fires when a fragment is removed.
        /// </summary>
        event EventHandler<IFragmentContext> RemoveFragment;

        /// <summary>
        /// Gets the collection of fragment contexts.
        /// </summary>
        IEnumerable<IFragmentContext> Fragments { get; }

        /// <summary>
        /// Returns all fragment contexts that belong to a given fragment type.
        /// </summary>
        /// <typeparam name="TFragment">The fragment type.</typeparam>
        /// <returns>An enumeration of the filtered fragment contexts.</returns>
        IEnumerable<IFragmentContext> GetFragments<TFragment>() where TFragment : IFragmentBase;

        /// <summary>
        /// Returns all fragment contexts that belong to a given fragment type.
        /// </summary>
        /// <param name="fragmentType">The fragment type.</param>
        /// <returns>An enumeration of the filtered fragment contexts.</returns>
        IEnumerable<IFragmentContext> GetFragments(Type fragmentType);

        /// <summary>
        /// Returns all fragment contexts that belong to a given fragment type.
        /// </summary>
        /// <typeparam name="TFragment">The fragment type..</typeparam>
        /// <param name="applicationContext">The application context.</param>
        /// <returns>An enumeration of the filtered fragment contexts.</returns>
        IEnumerable<IFragmentContext> GetFragments<TFragment>(IApplicationContext applicationContext)
            where TFragment : IFragmentBase;

        /// <summary>
        /// Returns all fragment contexts that belong to a given fragment type.
        /// </summary>
        /// <param name="applicationContext">The application context.</param>
        /// <param name="fragmentType">The fragment type.</param>
        /// <returns>An enumeration of the filtered fragment contexts.</returns>
        IEnumerable<IFragmentContext> GetFragments(IApplicationContext applicationContext, Type fragmentType);

        /// <summary>
        /// Returns all fragment contexts that belong to a given application.
        /// </summary>
        /// <typeparam name="TSection">The section where the fragment is embedded.</typeparam>
        /// <typeparam name="TScope">The scope where the fragment is embedded.</typeparam>
        /// <param name="applicationContext">The application context.</param>
        /// <returns>An enumeration of the filtered fragment contexts.</returns>
        IEnumerable<IFragmentContext> GetFragments<TSection, TScope>(IApplicationContext applicationContext)
            where TSection : ISection
            where TScope : IScope;

        /// <summary>
        /// Returns all fragment contexts that belong to a given application.
        /// </summary>
        /// <param name="applicationContext">The application context.</param>
        /// <param name="section">The section where the fragment is embedded.</param>
        /// <param name="scope">The scope where the fragment is embedded.</param>
        /// <returns>An enumeration of the filtered fragment contexts.</returns>
        IEnumerable<IFragmentContext> GetFragments(IApplicationContext applicationContext, Type section, Type scope);

        /// <summary>
        /// Returns all fragments that belong to a given application.
        /// </summary>
        /// <typeparam name="TFragment">The fragment type.</typeparam>
        /// <typeparam name="TSection">The section where the fragment is embedded.</typeparam>
        /// <param name="applicationContext">The application context.</param>
        /// <param name="scopes">The scopes where the fragment is embedded.</param>
        /// <returns>An enumeration of the filtered fragments.</returns>
        public IEnumerable<TFragment> GetFragments<TFragment, TSection>(IApplicationContext applicationContext, IEnumerable<Type> scopes)
            where TFragment : IFragmentBase
            where TSection : ISection;

        /// <summary>
        /// Returns all fragments that belong to a given page.
        /// </summary>
        /// <typeparam name="TFragment">The fragment type.</typeparam>
        /// <typeparam name="TSection">The section where the fragment is embedded.</typeparam>
        /// <param name="pageContext">The page context.</param>
        /// <returns>An enumeration of the filtered fragments.</returns>
        IEnumerable<TFragment> GetFragments<TFragment, TSection>(IPageContext pageContext)
            where TFragment : IFragmentBase
            where TSection : ISection;

        /// <summary>
        /// Returns the fragments of a page that may appear for the given request. A control that
        /// only reads properties of a fragment, instead of rendering it, never reaches the check in
        /// the fragment's own render method, so the conditions and policies are evaluated here.
        /// </summary>
        /// <typeparam name="TFragment">The fragment type.</typeparam>
        /// <typeparam name="TSection">The section where the fragment is embedded.</typeparam>
        /// <param name="pageContext">The page context.</param>
        /// <param name="request">The request whose state and identity decide which fragments are shown.</param>
        /// <returns>An enumeration of the fragments whose conditions and policies the request fulfills.</returns>
        IEnumerable<TFragment> GetFragments<TFragment, TSection>(IPageContext pageContext, IRequest request)
            where TFragment : IFragmentBase
            where TSection : ISection;

        /// <summary>
        /// Returns all fragment contexts that belong to a given application.
        /// </summary>
        /// <param name="applicationContext">The application context.</param>
        /// <param name="section">The section where the fragment is embedded.</param>
        /// <param name="scopes">The scopes where the fragment is embedded.</param>
        /// <returns>An enumeration of the filtered fragment contexts.</returns>
        IEnumerable<IFragmentContext> GetFragments(IApplicationContext applicationContext, Type section, IEnumerable<Type> scopes);

        /// <summary>
        /// Converts the fragments to HTML for a given section within the specified render context.
        /// </summary>
        /// <typeparam name="TRenderContext">The type of the render context.</typeparam>
        /// <typeparam name="TVisualTree">The type of the visual tree.</typeparam>
        /// <param name="renderContext">The context in which rendering occurs.</param>
        /// <param name="visualTree">The visual tree used for rendering.</param>
        /// <param name="section">The section where the fragment is embedded.</param>
        /// <returns>An enumeration of HTML nodes representing the rendered fragments.</returns>
        IEnumerable<IHtmlNode> Render<TRenderContext, TVisualTree>(TRenderContext renderContext, TVisualTree visualTree, Type section)
            where TRenderContext : IRenderContext
            where TVisualTree : IVisualTree;
    }
}
