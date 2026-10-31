using Microsoft.AspNetCore.Http.Features;
using System;
using System.Net;
using System.Text;

namespace WebExpress.WebCore.WebMessage
{
    /// <summary>
    /// Represents the context of an HTTP request and response.
    /// </summary>
    public class HttpContext : IHttpContext
    {
        /// <summary>
        /// Gets the context of the web server.
        /// </summary>
        public IHttpServerContext HttpServerContext { get; protected set; }

        /// <summary>
        /// Gets the id.
        /// </summary>
        public string Id { get; protected set; }

        /// <summary>
        /// Gets the request.
        /// </summary>
        public IRequest Request { get; protected set; }

        /// <summary>
        /// Gets the ip address and port number of the server to which the request is made.
        /// </summary>
        public EndPoint LocalEndPoint { get; protected set; }

        /// <summary>
        /// Gets the ip address and port number of the client from which the request originated.
        /// </summary>
        public EndPoint RemoteEndPoint { get; protected set; }

        /// <summary>
        /// Getsthe set of features.
        /// </summary>
        public IFeatureCollection Features { get; protected set; }

        /// <summary>
        /// Gets the encoding.
        /// </summary>
        public Encoding Encoding { get; protected set; } = Encoding.Default;

        /// <summary>
        /// Gets the uri.
        /// </summary>
        public Uri Uri { get; internal set; }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        internal HttpContext()
        {
        }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="contextFeatures">Initial set of features.</param>
        /// <param name="httpServerContext">The context of the Web server.</param>
        public HttpContext(IFeatureCollection contextFeatures, IHttpServerContext httpServerContext)
        {
            var connectionFeature = contextFeatures.Get<IHttpConnectionFeature>();
            var requestFeature = contextFeatures.Get<IHttpRequestFeature>();
            var header = new RequestHeaderFields(contextFeatures);
            var baseUri = new UriBuilder(requestFeature.Scheme, HostNameOf(header.Host), connectionFeature.LocalPort).Uri;

            Features = contextFeatures;
            Id = connectionFeature.ConnectionId;
            LocalEndPoint = new IPEndPoint(connectionFeature.LocalIpAddress, connectionFeature.LocalPort);
            RemoteEndPoint = new IPEndPoint(connectionFeature.RemoteIpAddress, connectionFeature.RemotePort);

            Encoding = requestFeature.Headers.ContentEncoding.Count != 0
                ? Encoding.GetEncoding(requestFeature.Headers.ContentEncoding)
                : Encoding.Default;
            Uri = new Uri(baseUri, requestFeature.RawTarget);

            Request = new Request(contextFeatures, header, httpServerContext);
        }

        /// <summary>
        /// Returns the host name of a host header, without the port it may carry.
        /// </summary>
        /// <remarks>
        /// A client addressing a non-default port sends it in the host header
        /// ("localhost:8080"), while the port of the connection is known separately. Handing
        /// the header over unchanged makes <see cref="UriBuilder"/> append a second port and
        /// the resulting "localhost:8080:8080" cannot be parsed - every request on such a port
        /// then fails before its context exists. An ipv6 literal is bracketed ("[::1]:8080"),
        /// so the separator only counts when it follows the closing bracket.
        /// </remarks>
        /// <param name="host">The value of the host header. May be null or empty.</param>
        /// <returns>The bare host name.</returns>
        internal static string HostNameOf(string host)
        {
            if (string.IsNullOrEmpty(host))
            {
                return host;
            }

            var separator = host.LastIndexOf(':');

            return separator > host.LastIndexOf(']')
                ? host[..separator]
                : host;
        }
    }
}
