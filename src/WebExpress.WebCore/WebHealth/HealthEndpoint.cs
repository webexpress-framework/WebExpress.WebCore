using Microsoft.AspNetCore.Http.Features;
using System;
using System.Threading;
using System.Threading.Tasks;
using WebExpress.WebCore.WebLog;
using WebExpress.WebCore.WebMessage;

namespace WebExpress.WebCore.WebHealth
{
    /// <summary>
    /// Provides a global probe contract independent of authentication, application routing, and status pages.
    /// </summary>
    internal static class HealthEndpoint
    {
        /// <summary>
        /// Reserves only the global health paths so similarly named application routes remain reachable.
        /// </summary>
        /// <param name="context">The context whose raw features remain available after request parsing failures.</param>
        /// <returns>True for the global health or liveness path, with an optional trailing slash.</returns>
        internal static bool Matches(IHttpContext context)
        {
            return RequestPath(context) is "/health" or "/health/" || IsLiveness(context);
        }

        /// <summary>
        /// Separates the liveness probe, which must not depend on application dependencies.
        /// </summary>
        /// <param name="context">The context whose raw features remain available after request parsing failures.</param>
        /// <returns>True for the liveness path, with an optional trailing slash.</returns>
        internal static bool IsLiveness(IHttpContext context)
        {
            return RequestPath(context) is "/health/live" or "/health/live/";
        }

        /// <summary>
        /// Reads the path from the raw features, which exist even when request parsing failed.
        /// </summary>
        /// <param name="context">The context of the probe request.</param>
        /// <returns>The path without query, or null when the context carries none.</returns>
        internal static string RequestPath(IHttpContext context)
        {
            var feature = context?.Features.Get<IHttpRequestFeature>();
            return string.IsNullOrEmpty(feature?.Path) ? feature?.RawTarget?.Split('?')[0] : feature.Path;
        }

        /// <summary>
        /// Produces stable probe responses while keeping every diagnostic detail in the server log.
        /// </summary>
        /// <param name="request">The probe request defining GET or HEAD semantics.</param>
        /// <param name="manager">The health manager, or null while the framework is unavailable.</param>
        /// <param name="log">The server log receiving unexpected framework errors.</param>
        /// <param name="cancellationToken">The cancellation token for the disconnected probe client.</param>
        /// <param name="liveness">Whether only the framework is judged, leaving application checks out.</param>
        /// <returns>A non-cacheable response containing only the public aggregate status.</returns>
        internal static async Task<IResponse> HandleAsync(IRequest request, IHealthManager manager, ILog log,
            CancellationToken cancellationToken, bool liveness = false)
        {
            if (request.Method is not RequestMethod.GET and not RequestMethod.HEAD)
            {
                var rejected = new ResponseMethodNotAllowed { Content = "{\"status\":\"method_not_allowed\"}" };
                rejected.Header.ContentType = "application/json; charset=utf-8";
                rejected.Header.CacheControl = "no-store";
                rejected.Header.CustomHeader["Allow"] = "GET, HEAD";
                return rejected;
            }

            var healthy = false;
            try
            {
                if (manager is null)
                {
                    log?.Error("The health manager is unavailable.");
                }
                else
                {
                    healthy = liveness ? manager.CheckLiveness() : await manager.CheckAsync(cancellationToken);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // disconnected clients do not indicate a dependency failure
            }
            catch (Exception ex)
            {
                log?.Exception(ex);
            }

            return CreateResponse(healthy, request.Method == RequestMethod.HEAD);
        }

        /// <summary>
        /// Keeps diagnostics out of both normal failures and unexpected health endpoint errors.
        /// </summary>
        /// <param name="healthy">Whether all critical checks succeeded.</param>
        /// <param name="head">Whether the response must omit its body.</param>
        /// <returns>A fixed JSON response using HTTP 200 or HTTP 503.</returns>
        internal static IResponse CreateResponse(bool healthy, bool head)
        {
            Response response = healthy ? new ResponseOK() : new ResponseServiceUnavailable();
            response.Content = head ? null : healthy
                ? "{\"status\":\"healthy\"}"
                : "{\"status\":\"unhealthy\",\"message\":\"One or more critical components are unavailable.\"}";
            response.Header.ContentType = "application/json; charset=utf-8";
            response.Header.CacheControl = "no-store";
            return response;
        }
    }
}
