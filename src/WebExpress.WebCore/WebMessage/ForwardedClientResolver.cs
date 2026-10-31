using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using WebExpress.WebCore.WebSetting;

namespace WebExpress.WebCore.WebMessage
{
    /// <summary>
    /// Determines the address of the client behind a request. Behind a reverse proxy the
    /// connection always comes from the proxy, so the client is only named by the
    /// X-Forwarded-For header - which, being an ordinary header, any caller can also forge.
    /// The header is therefore honoured only for connections from configured proxies.
    /// </summary>
    public sealed class ForwardedClientResolver
    {
        private readonly List<IPNetwork> _trustedProxies;

        /// <summary>
        /// Gets the networks whose X-Forwarded-For header is honoured.
        /// </summary>
        public IEnumerable<IPNetwork> TrustedProxies => _trustedProxies;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="settings">The configured overrides, or null for the built-in defaults.</param>
        public ForwardedClientResolver(SecuritySettings settings)
        {
            _trustedProxies = (settings?.TrustedProxies ?? [])
                .Select(ParseNetwork)
                .Where(x => x is not null)
                .Select(x => x.Value)
                .ToList();
        }

        /// <summary>
        /// Returns the address of the client that sent the request.
        /// </summary>
        /// <param name="request">The incoming request.</param>
        /// <returns>The client address, or null when the connection has none.</returns>
        public IPAddress Resolve(IRequest request)
        {
            var peer = (request?.RemoteEndPoint as IPEndPoint)?.Address;

            if (peer is null || !IsTrusted(Normalize(peer)))
            {
                return peer;
            }

            // every proxy appends the address it received the request from, so the chain is read
            // from the right: the first entry no trusted proxy accounts for is the client. entries
            // further left were written by the client itself and prove nothing
            var hops = (request.Header?.XForwardedFor ?? string.Empty).Split(',');

            for (var i = hops.Length - 1; i >= 0; i--)
            {
                if (!IPAddress.TryParse(hops[i].Trim(), out var hop))
                {
                    // a malformed chain names no one reliably; attributing the request to the
                    // proxy keeps it limited instead of letting it pick a bucket
                    return peer;
                }

                hop = Normalize(hop);

                if (!IsTrusted(hop))
                {
                    return hop;
                }
            }

            return peer;
        }

        /// <summary>
        /// Determines whether an address belongs to a configured proxy.
        /// </summary>
        /// <param name="address">The normalized address.</param>
        /// <returns>True when the address is a trusted proxy.</returns>
        private bool IsTrusted(IPAddress address)
        {
            return _trustedProxies.Any(x => x.Contains(address));
        }

        /// <summary>
        /// Parses a configured proxy, accepting a single address as well as a range in CIDR notation.
        /// </summary>
        /// <param name="value">The configured value.</param>
        /// <returns>The network, or null when the value is not an address or range.</returns>
        private static IPNetwork? ParseNetwork(string value)
        {
            var text = value?.Trim();

            if (IPNetwork.TryParse(text, out var network))
            {
                return network;
            }

            if (IPAddress.TryParse(text, out var address))
            {
                address = Normalize(address);
                return new IPNetwork(address, address.AddressFamily == AddressFamily.InterNetwork ? 32 : 128);
            }

            return null;
        }

        /// <summary>
        /// Maps an IPv4 address that a dual-stack socket reports in its IPv6 form back to IPv4,
        /// so it compares equal to the configured IPv4 proxy.
        /// </summary>
        /// <param name="address">The address.</param>
        /// <returns>The normalized address.</returns>
        private static IPAddress Normalize(IPAddress address)
        {
            return address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;
        }
    }
}
