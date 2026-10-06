using Microsoft.AspNetCore.Http.Features;
using WebExpress.WebCore.WebHealth;
using WebExpress.WebCore.WebMessage;

namespace WebExpress.WebCore.WebCluster
{
    /// <summary>
    /// Receives the messages other instances forward over http. It answers before any routing,
    /// like the health endpoint, so neither an application route nor the session handling sees
    /// these requests - an instance-to-instance call must not create a session or count as traffic.
    /// </summary>
    internal static class ClusterBusEndpoint
    {
        /// <summary>
        /// Determines whether a request targets the endpoint.
        /// </summary>
        /// <param name="context">The http context.</param>
        /// <returns>True for the bus path.</returns>
        internal static bool Matches(IHttpContext context)
        {
            return HealthEndpoint.RequestPath(context) == HttpClusterTransport.Path;
        }

        /// <summary>
        /// Verifies and dispatches a forwarded message.
        /// </summary>
        /// <param name="context">The http context carrying the signed body.</param>
        /// <param name="transport">The transport that verifies and dispatches the message.</param>
        /// <returns>The response for the sending instance.</returns>
        internal static IResponse Handle(IHttpContext context, HttpClusterTransport transport)
        {
            var feature = context.Features.Get<IHttpRequestFeature>();

            if (feature?.Method != "POST")
            {
                var rejected = new ResponseMethodNotAllowed();
                rejected.Header.CustomHeader["Allow"] = "POST";

                return rejected;
            }

            var signature = feature.Headers?[HttpClusterTransport.SignatureHeader].ToString();
            var body = (context.Request as Request)?.Content;

            // the same answer for every refusal, so a prober learns nothing about which check failed
            return transport.Receive(body, signature) ? new ResponseNoContent() : new ResponseForbidden();
        }
    }
}
