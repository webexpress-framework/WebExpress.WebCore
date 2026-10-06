using System.Collections.Generic;
using WebExpress.WebCore.WebEmail;

namespace WebExpress.WebCore.WebSetting
{
    /// <summary>
    /// Separates deployment credentials from application messages and provider implementations.
    /// </summary>
    public sealed class EmailProfileSettings
    {
        /// <summary>
        /// Gets or sets the registered provider name.
        /// </summary>
        public string Provider { get; set; } = "smtp";

        /// <summary>
        /// Gets or sets the sender used when the message omits its own sender.
        /// </summary>
        public string From { get; set; }

        /// <summary>
        /// Gets or sets the SMTP server hostname.
        /// </summary>
        public string Host { get; set; }

        /// <summary>
        /// Gets or sets the SMTP port independently of the TLS mode.
        /// </summary>
        public int Port { get; set; } = 587;

        /// <summary>
        /// Gets or sets the required transport protection without opportunistic TLS fallback.
        /// </summary>
        public EmailSecurity Security { get; set; } = EmailSecurity.StartTls;

        /// <summary>
        /// Gets or sets the optional SMTP account name.
        /// </summary>
        public string UserName { get; set; }

        /// <summary>
        /// Gets or sets the SMTP password supplied through deployment secrets.
        /// </summary>
        public string Password { get; set; }

        /// <summary>
        /// Gets or sets the total delivery budget, including connection and authentication.
        /// </summary>
        public int TimeoutSeconds { get; set; } = 60;

        /// <summary>
        /// Gets or sets provider-specific configuration for implementations supplied by plugins.
        /// </summary>
        public Dictionary<string, string> Options { get; set; } = [];
    }
}
