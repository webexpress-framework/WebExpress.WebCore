using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using System;
using System.Net;
using System.Text;
using System.Threading;

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
        /// <remarks>
        /// The request body is read here, synchronously. That suits a body already held in
        /// memory; the server itself builds its contexts without the body and reads it
        /// asynchronously once the request is being processed.
        /// </remarks>
        public HttpContext(IFeatureCollection contextFeatures, IHttpServerContext httpServerContext)
            : this(contextFeatures, httpServerContext, readContent: true)
        {
        }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="contextFeatures">Initial set of features.</param>
        /// <param name="httpServerContext">The context of the Web server.</param>
        /// <param name="readContent">
        /// True to read the request body right away; false leaves it to
        /// <see cref="WebMessage.Request.ReadContentAsync"/>, as a context Kestrel creates
        /// must not block on a body that is still arriving.
        /// </param>
        internal HttpContext(IFeatureCollection contextFeatures, IHttpServerContext httpServerContext, bool readContent)
        {
            var connectionFeature = contextFeatures.Get<IHttpConnectionFeature>();
            var requestFeature = contextFeatures.Get<IHttpRequestFeature>();
            var header = new RequestHeaderFields(contextFeatures);
            var requestHost = new HostString(header.Host);
            var baseUri = new UriBuilder(requestFeature.Scheme, HostNameOf(header.Host), requestHost.Port ?? -1).Uri;

            Features = contextFeatures;
            Id = connectionFeature.ConnectionId;
            LocalEndPoint = new IPEndPoint(connectionFeature.LocalIpAddress, connectionFeature.LocalPort);
            RemoteEndPoint = new IPEndPoint(connectionFeature.RemoteIpAddress, connectionFeature.RemotePort);
            Uri = ResolveTarget(baseUri, requestFeature.RawTarget);

            var request = new Request(contextFeatures, header, httpServerContext);

            if (readContent)
            {
                request.ReadContentAsync(CancellationToken.None).GetAwaiter().GetResult();
            }

            Request = request;
        }

        /// <summary>
        /// Resolves the request target against the authority the request was addressed to.
        /// </summary>
        /// <remarks>
        /// The target is never resolved as a relative reference: one that starts with "//"
        /// ("//evil.com/admin") would then be read as a network-path reference and replace the
        /// host, so the routing would see "/admin" while a proxy in front saw a different path.
        /// An absolute-form target only contributes its path and query, and anything else
        /// (the asterisk of "OPTIONS *") addresses the root.
        /// </remarks>
        /// <param name="baseUri">The scheme, host and port of the request.</param>
        /// <param name="rawTarget">The request target as the client sent it.</param>
        /// <returns>The uri of the request.</returns>
        internal static Uri ResolveTarget(Uri baseUri, string rawTarget)
        {
            var target = rawTarget switch
            {
                null or "" => "/",
                _ when rawTarget.StartsWith('/') => rawTarget,
                _ when Uri.TryCreate(rawTarget, UriKind.Absolute, out var absolute) => absolute.PathAndQuery,
                _ => "/"
            };

            return new Uri(baseUri.GetLeftPart(UriPartial.Authority) + target);
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
