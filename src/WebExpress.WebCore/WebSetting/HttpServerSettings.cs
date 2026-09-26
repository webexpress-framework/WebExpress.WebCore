using System.Collections.Generic;

namespace WebExpress.WebCore.WebSetting
{
    /// <summary>
    /// The settings of the web server itself, bound from the <see cref="Section"/> section of the
    /// merged configuration. Everything that is not about the server - a plugin's own settings -
    /// lives under <see cref="PluginSection"/> instead, so the two can never collide even though
    /// every file in the settings directory is merged into one configuration.
    /// </summary>
    /// <remarks>
    /// The class is a plain options object: it carries no knowledge of where the values came from
    /// and is filled once at start-up by the configuration binder. Optional blocks stay
    /// <see langword="null"/> when they are absent, so a missing block never changes a default the
    /// deployment did not opt into.
    /// </remarks>
    public sealed class HttpServerSettings
    {
        /// <summary>
        /// The name of the configuration section the server settings are read from.
        /// </summary>
        public const string Section = "WebExpress";

        /// <summary>
        /// The name of the configuration section that holds the settings of all plugins, one
        /// child section per plugin id.
        /// </summary>
        public const string PluginSection = "Plugins";

        /// <summary>
        /// The endpoints the web server listens on. At least one is needed for the server to
        /// answer anything.
        /// </summary>
        public List<EndpointSettings> Endpoints { get; set; } = [];

        /// <summary>
        /// The public base URI of the server, e.g. <c>https://www.example.com/</c>. This can
        /// differ from a listener binding such as <c>http://0.0.0.0:8080/</c> when the server is
        /// deployed behind a reverse proxy.
        /// </summary>
        public string ExternalUri { get; set; }

        /// <summary>
        /// Gets or sets the shared certificate inventory and expiry warning policy used for production HTTPS.
        /// </summary>
        public CertificateManagerSettings Certificates { get; set; }

        /// <summary>
        /// Optional fine-tuning of the underlying Kestrel server, including all request limits. When
        /// the block is omitted the web server keeps its built-in defaults, so this block only ever
        /// applies values that are explicitly opted into.
        /// </summary>
        public KestrelSettings Kestrel { get; set; }

        /// <summary>
        /// Optional settings of the session and its cookie. When the block is omitted the built-in
        /// defaults apply, so this block only ever changes values explicitly opted into.
        /// </summary>
        public SessionSettings Session { get; set; }

        /// <summary>
        /// Defines the shared signing authority and lifetimes for session-independent authentication.
        /// </summary>
        public AuthenticationSettings Authentication { get; set; }

        /// <summary>
        /// Optional overrides of the security headers and cookie attributes. When the block is
        /// omitted the restrictive built-in defaults apply.
        /// </summary>
        public SecuritySettings Security { get; set; }

        /// <summary>
        /// The log settings. A missing block keeps logging switched off.
        /// </summary>
        public LogSettings Log { get; set; } = new();

        /// <summary>
        /// The directory the packages are installed to, relative to the working directory or absolute.
        /// </summary>
        public string PackagePath { get; set; }

        /// <summary>
        /// The directory of the static files, relative to the working directory or absolute.
        /// </summary>
        public string AssetPath { get; set; }

        /// <summary>
        /// The directory the applications keep their data in, relative to the working directory or absolute.
        /// </summary>
        public string DataPath { get; set; }

        /// <summary>
        /// The path prefix every application is served under, e.g. <c>wx</c> to reach an
        /// application at <c>http://host/wx/app</c>. Empty for none.
        /// </summary>
        public string ContextPath { get; set; }

        /// <summary>
        /// The culture the server runs in, as a culture name such as <c>en-US</c>. Empty to keep
        /// the culture of the operating system.
        /// </summary>
        public string Culture { get; set; }
    }
}
