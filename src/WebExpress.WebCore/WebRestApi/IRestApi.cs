using WebExpress.WebCore.WebEndpoint;

namespace WebExpress.WebCore.WebRestApi
{
    /// <summary>
    /// An endpoint that exposes a REST API: it is reachable at a route and typically exchanges data
    /// (such as JSON) rather than rendering an HTML page, for use by clients and scripts.
    /// </summary>
    public interface IRestApi : IEndpoint
    {
    }
}
