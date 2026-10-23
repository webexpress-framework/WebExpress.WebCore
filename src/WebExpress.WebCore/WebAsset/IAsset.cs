using WebExpress.WebCore.WebEndpoint;
using WebExpress.WebCore.WebMessage;

namespace WebExpress.WebCore.WebAsset
{
    /// <summary>
    /// An endpoint that serves a static asset — a file such as an image, script, or stylesheet that
    /// is delivered to the client largely as-is. Given a request it returns the asset as the response.
    /// </summary>
    public interface IAsset : IEndpoint
    {
        /// <summary>
        /// Processing of the resource.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The response.</returns>
        IResponse Process(IRequest request);
    }
}
