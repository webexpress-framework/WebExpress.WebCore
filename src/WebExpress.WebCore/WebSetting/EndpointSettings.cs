using Microsoft.AspNetCore.Http;
using System;
using System.Net;

namespace WebExpress.WebCore.WebSetting
{
    /// <summary>
    /// Defines a listener whose production HTTPS certificate is resolved by the central certificate manager.
    /// Development endpoints continue to use HTTP without certificate configuration.
    /// </summary>
    public sealed class EndpointSettings
    {
        /// <summary>
        /// The uri to listen on, e.g. <c>http://localhost/</c> or <c>https://*:443/</c>. The hosts
        /// <c>*</c> and <c>+</c> stand for every address of the machine.
        /// </summary>
        public string Uri { get; set; }

        /// <summary>
        /// Gets or sets an inline PFX reference relative to the configured certificate directory, or an absolute path.
        /// HTTPS endpoints can omit this when an alias or hostname resolves an inventory entry.
        /// </summary>
        public string PfxFile { get; set; }

        /// <summary>
        /// The password that unlocks the pfx file.
        /// </summary>
        public string Password { get; set; }

        /// <summary>
        /// Gets or sets the certificate alias, optionally naming an inline PFX definition for reuse.
        /// When omitted, the manager uses the inline endpoint identity or a configured hostname mapping.
        /// </summary>
        public string CertificateAlias { get; set; }

        /// <summary>
        /// Parses wildcard bindings consistently for certificate resolution and listener registration.
        /// </summary>
        /// <returns>The validated HTTP or HTTPS binding address.</returns>
        internal BindingAddress GetBindingAddress()
        {
            var address = BindingAddress.Parse(Uri);
            if ((!address.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase) &&
                !address.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase)) ||
                address.Port < 0 || address.Port > 65535)
            {
                throw new ArgumentException("An endpoint requires an HTTP or HTTPS address with a valid port.", nameof(Uri));
            }
            return address;
        }

        /// <summary>
        /// Determines whether a binding host stands for every address of the machine rather than
        /// naming one. Containers bind to <c>0.0.0.0</c> or <c>[::]</c> as a rule, and such a host
        /// names nothing a certificate could be issued for, so the hostname check must skip it.
        /// </summary>
        /// <param name="host">The host of a binding address.</param>
        /// <returns>True for <c>*</c>, <c>+</c>, <c>0.0.0.0</c> and <c>[::]</c>.</returns>
        internal static bool IsAnyHost(string host)
        {
            if (host is "*" or "+")
            {
                return true;
            }

            return IPAddress.TryParse(host?.Trim('[', ']'), out var address)
                && (address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any));
        }

        /// <summary>
        /// Conversion into its string representation.
        /// </summary>
        /// <returns>The uri of the endpoint.</returns>
        public override string ToString()
        {
            return Uri;
        }
    }
}
