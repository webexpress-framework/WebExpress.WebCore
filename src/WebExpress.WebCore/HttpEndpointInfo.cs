using Microsoft.AspNetCore.Server.Kestrel.Core;

namespace WebExpress.WebCore
{
    /// <summary>
    /// Describes a listening endpoint as the server actually opened it, so diagnostics show the
    /// served protocols rather than the configured ones.
    /// </summary>
    /// <param name="Endpoint">The ip address and port.</param>
    /// <param name="Tls">Whether the endpoint uses TLS.</param>
    /// <param name="Protocols">The HTTP protocols the endpoint serves.</param>
    public sealed record HttpEndpointInfo(string Endpoint, bool Tls, HttpProtocols Protocols);
}
