using System.Collections.Generic;
using System.Globalization;
using Microsoft.Extensions.Configuration;
using WebExpress.WebCore.WebCertificate;
using WebExpress.WebCore.WebEndpoint;
using WebExpress.WebCore.WebLog;
using WebExpress.WebCore.WebSetting;

namespace WebExpress.WebCore
{
    /// <summary>
    /// Provides the server-wide information that plugins and components need to do their work:
    /// the routing entry point, the configured endpoints, the server version, the well-known
    /// directories (packages, assets, data, settings), the culture, the configuration and the
    /// central log. A single instance is shared for the lifetime of the running server.
    /// </summary>
    public interface IHttpServerContext
    {
        /// <summary>
        /// Gets the shared certificate service so applications never need direct storage access.
        /// </summary>
        ICertificateManager CertificateManager { get; }

        /// <summary>
        /// Gets the route of the web server.
        /// </summary>
        IRoute Route { get; }

        /// <summary>
        /// Gets the endpoints to which the web server responds.
        /// </summary>
        ICollection<EndpointSettings> Endpoints { get; }

        /// <summary>
        /// Gets the version of the http(s) server.
        /// </summary>
        string Version { get; }

        /// <summary>
        /// Gets the package home directory.
        /// </summary>
        string PackagePath { get; }

        /// <summary>
        /// Gets the asset home directory.
        /// </summary>
        string AssetPath { get; }

        /// <summary>
        /// Gets the data home directory.
        /// </summary>
        string DataPath { get; }

        /// <summary>
        /// Gets the settings directory, the one place every settings file - the server's and the
        /// plugins' - is read from.
        /// </summary>
        string SettingsPath { get; }

        /// <summary>
        /// Gets the merged configuration of the server and all plugins. A plugin reads its own
        /// section through <see cref="WebPlugin.IPluginContext.Settings"/>; the root is exposed
        /// so the framework can reload it after a package deployed a new settings file.
        /// </summary>
        IConfigurationRoot Configuration { get; }

        /// <summary>
        /// Gets the culture.
        /// </summary>
        CultureInfo Culture { get; }

        /// <summary>
        /// Gets the log for writing status messages to the console and to a log file.
        /// </summary>
        ILog Log { get; }

        /// <summary>
        /// Gets the host.
        /// </summary>
        IHost Host { get; }
    }
}
