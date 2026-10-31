using WebExpress.WebCore.WebEndpoint;
using WebExpress.WebCore.WebMessage;

namespace WebExpress.WebCore.WebResource
{
    /// <summary>
    /// An endpoint that answers a request directly: given the incoming <see cref="IRequest"/> it
    /// produces an <see cref="IResponse"/>. This is the general-purpose building block for anything
    /// served at a route that is not specifically a rendered page or a REST API.
    /// </summary>
    public interface IResource : IEndpoint
    {
        /// <summary>
        /// Processing of the resource.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The response.</returns>
        IResponse Process(IRequest request);
    }
}
