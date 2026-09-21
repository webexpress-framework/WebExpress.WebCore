using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace WebExpress.WebCore.WebCertificate
{
    /// <summary>
    /// Isolates PFX file access and keeps platform-specific key storage out of certificate consumers.
    /// </summary>
    public sealed class FileCertificateStore : ICertificateStore
    {
        private readonly string _directory;

        /// <summary>
        /// Gets the store name for operational diagnostics.
        /// </summary>
        public string Name => "file";

        /// <summary>
        /// Anchors relative references once so loading does not depend on later working directory changes.
        /// </summary>
        /// <param name="directory">The base directory for relative PFX paths.</param>
        public FileCertificateStore(string directory)
        {
            _directory = Path.GetFullPath(string.IsNullOrWhiteSpace(directory) ? "." : directory);
        }

        /// <summary>
        /// Loads a certificate from a PFX file.
        /// </summary>
        /// <param name="reference">The path to the PFX file.</param>
        /// <param name="password">The password for the PFX file.</param>
        /// <returns>The loaded certificate material.</returns>
        public CertificateMaterial Load(string reference, string password)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(reference);
            // schannel needs a temporary key container on windows; disposal removes it
            var keyStorage = OperatingSystem.IsWindows() ? X509KeyStorageFlags.DefaultKeySet : X509KeyStorageFlags.EphemeralKeySet;
            var certificates = X509CertificateLoader.LoadPkcs12CollectionFromFile
            (
                Path.GetFullPath(reference, _directory), password, keyStorage
            );

            try
            {
                var leaves = certificates.Cast<X509Certificate2>().Where(x => x.HasPrivateKey).ToArray();
                if (leaves.Length > 1 || certificates.Count == 0)
                {
                    throw new CryptographicException("A PFX must identify exactly one server certificate.");
                }

                // keyless material remains inspectable through the manager's validation status
                var leaf = leaves.SingleOrDefault() ?? certificates[0];
                return new CertificateMaterial(leaf, certificates.Cast<X509Certificate2>());
            }
            catch
            {
                foreach (var certificate in certificates) { certificate.Dispose(); }
                throw;
            }
        }
    }
}
