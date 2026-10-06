using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebUri;

namespace WebExpress.WebCore.WebPage
{
    /// <summary>
    /// Default implementation of <see cref="IVisualTreeContext"/>. It derives the request and URI
    /// from the active render context, giving a page's control tree access to them while it is built.
    /// </summary>
    public class VisualTreeContext : IVisualTreeContext
    {
        /// <summary>
        /// Gets the request.
        /// </summary>
        public IRequest Request => RenderContext?.Request;

        /// <summary>
        /// Gets the uri of the request.
        /// </summary>
        public IUri Uri => RenderContext?.Request?.Uri;

        /// <summary>
        /// Gets or sets the render context.
        /// </summary>
        public IRenderContext RenderContext { get; protected set; }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="context">The context to copy.</param>
        public VisualTreeContext(IRenderContext context)
        {
            RenderContext = context;
        }
    }
}
