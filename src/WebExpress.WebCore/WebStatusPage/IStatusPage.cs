using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebPage;

namespace WebExpress.WebCore.WebStatusPage
{
    /// <summary>
    /// A component that renders the page shown for an HTTP status (for example the 404 or 500
    /// error page). Implement it to customize how a given status is presented to the user.
    /// </summary>
    public interface IStatusPage : IStatusPage<VisualTree>
    {

    }

    /// <summary>
    /// Defines the contract for a status page resource that can be rendered using a specific context.
    /// </summary>
    /// <typeparam name="T">The type of the render context.</typeparam>
    public interface IStatusPage<T> : IComponent where T : IVisualTree
    {
        /// <summary>
        /// Processing of the status page.
        /// </summary>
        /// <param name="context">The context for rendering the status page.</param>
        /// <param name="visualTree">The visual tree to be rendered.</param>
        void Process(IRenderContext context, T visualTree);
    }
}
