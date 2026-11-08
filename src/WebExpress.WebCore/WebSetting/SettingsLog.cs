using System;
using System.Linq;

namespace WebExpress.WebCore.WebSetting
{
    /// <summary>
    /// Keeps startup configuration diagnostics useful without copying credentials into logs.
    /// </summary>
    internal static class SettingsLog
    {
        /// <summary>
        /// Masks credential settings and escapes line breaks so each parameter occupies one log entry.
        /// </summary>
        /// <param name="key">The full configuration path used to identify credential sections.</param>
        /// <param name="value">The merged configuration value, or null when it is not configured.</param>
        /// <returns>The value suitable for startup diagnostics.</returns>
        internal static string FormatValue(string key, string value)
        {
            if (value is null)
            {
                return "(not configured)";
            }

            var sensitive = new[] { "password", "secret", "credential", "connectionstring", "signingkey", "privatekey", "apikey", "bearertoken", "accesstoken", "refreshtoken" };
            if (sensitive.Any(part => key.Contains(part, StringComparison.OrdinalIgnoreCase)))
            {
                return "[redacted]";
            }

            // uri settings can contain credentials even when their configuration key is harmless
            if (System.Uri.TryCreate(value, UriKind.Absolute, out var uri) && !string.IsNullOrEmpty(uri.UserInfo))
            {
                return "[redacted]";
            }

            return value.Replace("\r", "\\r").Replace("\n", "\\n");
        }
    }
}
