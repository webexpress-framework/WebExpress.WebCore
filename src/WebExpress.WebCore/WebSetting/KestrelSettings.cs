using System;
using Microsoft.AspNetCore.Server.Kestrel.Core;

namespace WebExpress.WebCore.WebSetting
{
    /// <summary>
    /// Optional fine-tuning of the underlying Kestrel server. The whole block as well as every
    /// individual property is optional: any value that is left unset (<see langword="null"/>) keeps
    /// the behavior the web server applied before this block existed, so adding the block can never
    /// change defaults a deployment did not explicitly opt into.
    /// </summary>
    public sealed class KestrelSettings
    {
        /// <summary>
        /// Allows synchronous IO on the request and response streams. WebExpress renders and sends
        /// responses synchronously, which is why this defaults to <c>true</c> when not specified.
        /// </summary>
        public bool? AllowSynchronousIO { get; set; }

        /// <summary>
        /// Allows the response headers to be compressed. Defaults to <c>true</c> when not specified.
        /// </summary>
        public bool? AllowResponseHeaderCompression { get; set; }

        /// <summary>
        /// Controls whether the <c>Server</c> response header is emitted. Disabling it reduces the
        /// information exposed about the host. Defaults to <c>true</c> when not specified.
        /// </summary>
        public bool? AddServerHeader { get; set; }

        /// <summary>
        /// The HTTP protocols enabled on every listening endpoint, given as the name of a Kestrel
        /// <see cref="HttpProtocols"/> value (e.g. <c>Http1</c>, <c>Http2</c> or <c>Http1AndHttp2</c>).
        /// When not specified, a TLS endpoint serves <c>Http1AndHttp2AndHttp3</c> - HTTP/3 over QUIC
        /// wherever the operating system provides it, announced to browsers via Alt-Svc - and a
        /// plain endpoint keeps the Kestrel default, serving HTTP/1.1. Configure
        /// <c>Http1AndHttp2</c> to keep a TLS endpoint off UDP. HTTP/3 is dropped automatically
        /// without TLS or QUIC support, so it never leaves an endpoint unreachable. Set <c>Http2</c> on a plain
        /// (non-TLS) endpoint to enable cleartext HTTP/2 (h2c), which has no automatic upgrade path.
        /// </summary>
        /// <remarks>
        /// Kept as text rather than bound to the enum directly, so a typo keeps the default instead
        /// of failing the start-up - see <see cref="ResolveProtocols"/>.
        /// </remarks>
        public string Protocols { get; set; }

        /// <summary>
        /// The maximum number of concurrent client connections. When not specified the Kestrel
        /// default (unlimited) is kept.
        /// </summary>
        public long? MaxConcurrentConnections { get; set; }

        /// <summary>
        /// The maximum allowed size of a request body, in bytes. When not specified the Kestrel
        /// default is kept.
        /// </summary>
        public long? MaxRequestBodySize { get; set; }

        /// <summary>
        /// The maximum allowed size of the combined request headers, in bytes. When not specified
        /// the Kestrel default (32 KiB) is kept.
        /// </summary>
        public int? MaxRequestHeadersTotalSize { get; set; }

        /// <summary>
        /// The maximum number of open, upgraded connections (e.g. WebSockets). Upgraded connections
        /// are not counted against the regular connection limit. When not specified the Kestrel
        /// default is kept.
        /// </summary>
        public long? MaxConcurrentUpgradedConnections { get; set; }

        /// <summary>
        /// The maximum size of the request buffer, in bytes. When not specified the Kestrel default is kept.
        /// </summary>
        public long? MaxRequestBufferSize { get; set; }

        /// <summary>
        /// The maximum size of the response buffer, in bytes. When not specified the Kestrel default is kept.
        /// </summary>
        public long? MaxResponseBufferSize { get; set; }

        /// <summary>
        /// The maximum allowed size of the request line (request method, uri and protocol), in bytes.
        /// When not specified the Kestrel default is kept.
        /// </summary>
        public int? MaxRequestLineSize { get; set; }

        /// <summary>
        /// The keep-alive timeout, in seconds. A value that closes idle connections after a period of
        /// inactivity. When not specified the Kestrel default is kept.
        /// </summary>
        public int? KeepAliveTimeout { get; set; }

        /// <summary>
        /// The amount of time, in seconds, the server waits for the request headers to be received in
        /// full before closing the connection. When not specified the Kestrel default is kept.
        /// </summary>
        public int? RequestHeadersTimeout { get; set; }

        /// <summary>
        /// Resolves the configured <see cref="Protocols"/> name to the corresponding Kestrel
        /// <see cref="HttpProtocols"/> value. Returns <see langword="null"/> when nothing was
        /// configured or the value is not a recognised protocol name, so the caller keeps the
        /// Kestrel default instead of silently applying an unintended restriction from a typo.
        /// </summary>
        /// <returns>The parsed protocols, or <see langword="null"/> to keep the Kestrel default.</returns>
        public HttpProtocols? ResolveProtocols()
        {
            if (string.IsNullOrWhiteSpace(Protocols))
            {
                return null;
            }

            // Enum.TryParse alone would accept arbitrary numbers for a flags enum, so the result is
            // additionally constrained to a named value to reject unknown or nonsensical combinations
            return Enum.TryParse<HttpProtocols>(Protocols, ignoreCase: true, out var result)
                && Enum.IsDefined(typeof(HttpProtocols), result)
                ? result
                : null;
        }
    }
}
