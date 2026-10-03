using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WebExpress.WebCore.WebHealt;
using WebExpress.WebCore.WebLog;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebMetrics.Model;
using WebExpress.WebCore.WebSetting;

namespace WebExpress.WebCore.WebMetrics
{
    /// <summary>
    /// Serves the Prometheus scrape target independent of application routing, sessions and status
    /// pages, so a scrape neither creates sessions nor is mistaken for user traffic.
    /// </summary>
    internal static class MetricsEndpoint
    {
        /// <summary>
        /// Reserves only the global metrics path so similarly named application routes remain reachable.
        /// </summary>
        /// <param name="context">The context whose raw features remain available after request parsing failures.</param>
        /// <returns>True for the global metrics path, with an optional trailing slash.</returns>
        internal static bool Matches(IHttpContext context)
        {
            return HealthEndpoint.RequestPath(context) is "/metrics" or "/metrics/";
        }

        /// <summary>
        /// Answers a scrape with the current metrics, keeping every failure detail in the server log.
        /// </summary>
        /// <param name="request">The scrape request defining GET or HEAD semantics.</param>
        /// <param name="manager">The metrics manager, or null while the framework is unavailable.</param>
        /// <param name="settings">The endpoint settings, or null for the defaults.</param>
        /// <param name="log">The server log receiving unexpected framework errors.</param>
        /// <param name="cancellationToken">The cancellation token for the disconnected scraper.</param>
        /// <returns>A non-cacheable response in the Prometheus text format.</returns>
        internal static async Task<IResponse> HandleAsync(IRequest request, IMetricsManager manager, MetricsSettings settings,
            ILog log, CancellationToken cancellationToken)
        {
            if (request.Method is not RequestMethod.GET and not RequestMethod.HEAD)
            {
                var rejected = CreateResponse(new ResponseMethodNotAllowed(), "method not allowed\n", false);
                rejected.Header.CustomHeader["Allow"] = "GET, HEAD";
                return rejected;
            }

            var head = request.Method == RequestMethod.HEAD;

            if (!IsAuthorized(request, settings?.BearerToken))
            {
                var unauthorized = CreateResponse(new ResponseUnauthorized(), "unauthorized\n", head);
                unauthorized.Header.WWWAuthenticate = false;
                unauthorized.Header.CustomHeader["WWW-Authenticate"] = "Bearer realm=\"metrics\"";
                return unauthorized;
            }

            try
            {
                if (manager is null)
                {
                    log?.Error("The metrics manager is unavailable.");
                }
                else
                {
                    var families = await manager.CollectAsync(cancellationToken);
                    return CreateResponse(new ResponseOK(), MetricText.Format(families), head);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // a disconnected scraper does not indicate a failure of the server
            }
            catch (Exception ex)
            {
                log?.Exception(ex);
            }

            return CreateUnavailable(head);
        }

        /// <summary>
        /// Answers a scrape the server cannot serve, without exposing why.
        /// </summary>
        /// <param name="head">Whether the response must omit its body.</param>
        /// <returns>A plain HTTP 503 response.</returns>
        internal static IResponse CreateUnavailable(bool head)
        {
            return CreateResponse(new ResponseServiceUnavailable(), "metrics unavailable\n", head);
        }

        /// <summary>
        /// Compares the presented token in constant time, since the endpoint may be reachable by
        /// anyone who can reach the server.
        /// </summary>
        /// <param name="request">The scrape request.</param>
        /// <param name="token">The configured token, or null when the endpoint is open.</param>
        /// <returns>True when no token is configured or the request presents it.</returns>
        private static bool IsAuthorized(IRequest request, string token)
        {
            if (string.IsNullOrEmpty(token))
            {
                return true;
            }

            var authorization = request.Header.Authorization;
            if (!string.Equals(authorization?.Type, "Bearer", StringComparison.OrdinalIgnoreCase) || authorization.Token is null)
            {
                return false;
            }

            // hashed first so the comparison takes the same time whatever the length of the guess
            return CryptographicOperations.FixedTimeEquals(
                SHA256.HashData(Encoding.UTF8.GetBytes(authorization.Token)),
                SHA256.HashData(Encoding.UTF8.GetBytes(token)));
        }

        /// <summary>
        /// Applies the content type and caching rules shared by every metrics response.
        /// </summary>
        /// <param name="response">The response to complete.</param>
        /// <param name="content">The body.</param>
        /// <param name="head">Whether the response must omit its body.</param>
        /// <returns>The completed response.</returns>
        private static Response CreateResponse(Response response, string content, bool head)
        {
            response.Content = head ? null : content;
            response.Header.ContentType = MetricText.ContentType;
            response.Header.CacheControl = "no-store";
            return response;
        }
    }
}
