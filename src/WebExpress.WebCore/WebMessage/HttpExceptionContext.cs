using Microsoft.AspNetCore.Http.Features;
using System;
using System.Net;

namespace WebExpress.WebCore.WebMessage
{
    /// <summary>
    /// A fallback <see cref="HttpContext"/> used when a normal request context could not be built
    /// (for example, the request was malformed). It still carries the basic connection details and
    /// additionally holds the <see cref="Exception"/> that caused the failure, so an error response
    /// can be produced.
    /// </summary>
    public class HttpExceptionContext : HttpContext
    {
        /// <summary>
        /// Gets or sets an error message if the context could not be created.
        /// </summary>
        public Exception Exception { get; private set; }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="exception">An exception that prevented the creation of the context.</param>
        /// <param name="contextFeatures">Initial set of features.</param>
        public HttpExceptionContext(Exception exception, IFeatureCollection contextFeatures)
        {
            var connectionFeature = contextFeatures.Get<IHttpConnectionFeature>();
            //var requestFeature = contextFeatures.Get<IHttpRequestFeature>();

            Features = contextFeatures;
            Id = connectionFeature.ConnectionId;

            LocalEndPoint = new IPEndPoint(connectionFeature.LocalIpAddress, connectionFeature.LocalPort);
            RemoteEndPoint = new IPEndPoint(connectionFeature.RemoteIpAddress, connectionFeature.RemotePort);

            Exception = exception;
        }
    }
}
