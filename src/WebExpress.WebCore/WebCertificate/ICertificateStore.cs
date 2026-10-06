namespace WebExpress.WebCore.WebCertificate
{
    /// <summary>
    /// Allows modules to supply certificate material without exposing their storage to applications.
    /// </summary>
    public interface ICertificateStore
    {
        /// <summary>
        /// Gets the case-insensitive name selected by certificate configuration.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Transfers newly owned certificate material to the manager for validation and lifetime management.
        /// </summary>
        /// <param name="reference">The identifier interpreted by this store.</param>
        /// <param name="password">The optional credential needed to read the material.</param>
        /// <returns>The material whose certificates must not be disposed by the store after return.</returns>
        CertificateMaterial Load(string reference, string password);
    }
}
