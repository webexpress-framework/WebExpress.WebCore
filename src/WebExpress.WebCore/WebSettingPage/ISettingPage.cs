using WebExpress.WebCore.WebEndpoint;
using WebExpress.WebCore.WebPage;

namespace WebExpress.WebCore.WebSettingPage
{
    /// <summary>
    /// An endpoint that renders a page in the application's settings area. Implement it to add a
    /// configuration page; the framework places it within the settings navigation and renders it.
    /// </summary>
    public interface ISettingPage : ISettingPage<VisualTree>
    {
    }

    /// <summary>
    /// Defines the contract for a setting page resource that can be rendered using a specific context.
    /// </summary>
    /// <typeparam name="TVisualTree">The type of the visual tree.</typeparam>
    public interface ISettingPage<TVisualTree> : IEndpoint where TVisualTree : IVisualTree
    {
        /// <summary>
        /// Processing of the page.
        /// </summary>
        /// <param name="renderContext">The context for rendering the setting page.</param>
        /// <param name="visualTree">The visual tree to be rendered.</param>
        void Process(IRenderContext renderContext, TVisualTree visualTree);
    }
}
