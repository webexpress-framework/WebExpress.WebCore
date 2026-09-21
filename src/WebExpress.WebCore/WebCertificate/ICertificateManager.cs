using System;
using System.Collections.Generic;
using WebExpress.WebCore.WebSetting;

namespace WebExpress.WebCore.WebCertificate
{
    /// <summary>
    /// Provides the only certificate loading and resolution boundary used by applications and hosts.
    /// </summary>
    public interface ICertificateManager : IDisposable
    {
        /// <summary>
        /// Registers a store supplied by a module before its certificates are loaded.
        /// </summary>
        /// <param name="store">The externally owned store whose name must be unique.</param>
        void RegisterStore(ICertificateStore store);

        /// <summary>
        /// Loads the configured inventory and endpoint certificates before listeners are started.
        /// Previously returned material remains alive until this manager is disposed.
        /// </summary>
        /// <param name="settings">The server configuration containing certificates and endpoint references.</param>
        void Load(HttpServerSettings settings);

        /// <summary>
        /// Resolves usable material by a case-insensitive alias or configured hostname.
        /// </summary>
        /// <param name="aliasOrHostName">The configured lookup key.</param>
        /// <returns>Borrowed material that the caller must not modify or dispose.</returns>
        CertificateMaterial Resolve(string aliasOrHostName);

        /// <summary>
        /// Resolves usable material and verifies coverage of a concrete HTTPS endpoint hostname.
        /// </summary>
        /// <param name="endpoint">The endpoint whose alias takes precedence over its hostname.</param>
        /// <returns>Borrowed material that the caller must not modify or dispose.</returns>
        CertificateMaterial Resolve(EndpointSettings endpoint);

        /// <summary>
        /// Supplies current metadata and status without disclosing private material.
        /// </summary>
        /// <returns>A snapshot of every configured certificate, including failed entries.</returns>
        IReadOnlyList<CertificateInfo> GetCertificates();
    }
}
