using Microsoft.AspNetCore.Http;
using System;

namespace WebExpress.WebCore.WebSetting
{
    /// <summary>
    /// Defines a listener whose production HTTPS certificate is resolved by the central certificate manager.
    /// Development endpoints continue to use HTTP without certificate configuration.
    /// </summary>
    public sealed class EndpointSettings
    {
        /// <summary>
        /// The uri to listen on, e.g. <c>http://localhost/</c> or <c>https://*:443/</c>. The host
        /// <c>*</c> stands for every address of the machine.
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
        /// Conversion into its string representation.
        /// </summary>
        /// <returns>The uri of the endpoint.</returns>
        public override string ToString()
        {
            return Uri;
        }
    }
}
