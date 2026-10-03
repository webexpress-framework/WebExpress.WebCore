using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;

namespace WebExpress.WebCore.WebCertificate
{
    /// <summary>
    /// Keeps a server certificate and its supplied chain alive for the same hosting lifetime.
    /// Consumers borrow this material and must not dispose or modify its certificates.
    /// </summary>
    public sealed class CertificateMaterial : IDisposable
    {
        private bool _disposed;

        /// <summary>
        /// Gets the leaf certificate that authenticates the server.
        /// </summary>
        public X509Certificate2 Certificate { get; }

        /// <summary>
        /// Gets the additional certificates that allow the host to send the supplied chain.
        /// </summary>
        public IReadOnlyList<X509Certificate2> Chain { get; }

        /// <summary>
        /// Takes ownership of the supplied certificates so a store can transfer them as one unit.
        /// </summary>
        /// <param name="certificate">The leaf certificate, normally including its private key.</param>
        /// <param name="chain">The optional issuer certificates owned by this material.</param>
        public CertificateMaterial(X509Certificate2 certificate, IEnumerable<X509Certificate2> chain = null)
        {
            ArgumentNullException.ThrowIfNull(certificate);
            Certificate = certificate;
            Chain = Array.AsReadOnly((chain ?? []).Where(x => !ReferenceEquals(x, certificate))
                .Distinct<X509Certificate2>(ReferenceEqualityComparer.Instance).ToArray());
        }

        /// <summary>
        /// Releases the native certificate handles after all hosting consumers have stopped.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) { return; }
            _disposed = true;
            Certificate.Dispose();
            foreach (var certificate in Chain) { certificate.Dispose(); }
        }
    }
}
