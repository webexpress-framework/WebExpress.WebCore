using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebUri;

namespace WebExpress.WebCore.WebPage
{
    /// <summary>
    /// Supplies the per-request information a visual tree needs while a page is being built — most
    /// importantly the current request and URI. (A visual tree is the structure of controls that
    /// makes up a page.)
    /// </summary>
    public interface IVisualTreeContext
    {
        /// <summary>
        /// Gets the request.
        /// </summary>
        IRequest Request { get; }

        /// <summary>
        /// Gets the uri of the request.
        /// </summary>
        IUri Uri { get; }

        /// <summary>
        /// Gets the render context.
        /// </summary>
        IRenderContext RenderContext { get; }
    }
}
