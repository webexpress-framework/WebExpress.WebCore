using System;
using System.Text;

namespace WebExpress.WebCore.WebHtml
{
    /// <summary>
    /// Decides whether an address taken from user content may be written into a link or an image.
    /// </summary>
    /// <remarks>
    /// A link written from user content runs whatever its address says when it is followed, so
    /// <c>javascript:</c>, <c>vbscript:</c> or a <c>data:</c> document turn a link into script.
    /// Addresses are therefore allowed by an allowlist of schemes, never by a list of bad ones.
    /// The scheme is read the way a browser reads it: whitespace and control characters inside it
    /// are ignored (<c>java&#9;script:</c> is <c>javascript:</c>) and its case does not matter.
    /// </remarks>
    public static class UrlPolicy
    {
        /// <summary>
        /// Tells whether an address may be the target of a link: a relative address or one with the
        /// scheme http, https, mailto or tel.
        /// </summary>
        /// <param name="url">The address, decoded from any entity references.</param>
        /// <returns>True if the address is safe to follow.</returns>
        public static bool IsSafeLink(string url)
        {
            return Scheme(url) is { } scheme && scheme is "" or "http" or "https" or "mailto" or "tel";
        }

        /// <summary>
        /// Tells whether an address may be the source of an image: a relative address, one with
        /// the scheme http or https, or a data address of a raster image.
        /// </summary>
        /// <param name="url">The address, decoded from any entity references.</param>
        /// <returns>True if the address is safe to load as an image.</returns>
        public static bool IsSafeImage(string url)
        {
            var scheme = Scheme(url);

            if (scheme is "" or "http" or "https")
            {
                return true;
            }

            if (scheme != "data")
            {
                return false;
            }

            // an svg can carry script, so only raster formats are taken from a data address
            var normalized = Normalize(url);

            return normalized.StartsWith("data:image/png", StringComparison.OrdinalIgnoreCase)
                || normalized.StartsWith("data:image/jpeg", StringComparison.OrdinalIgnoreCase)
                || normalized.StartsWith("data:image/gif", StringComparison.OrdinalIgnoreCase)
                || normalized.StartsWith("data:image/webp", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Reads the scheme of an address.
        /// </summary>
        /// <param name="url">The address.</param>
        /// <returns>The scheme in lower case, an empty string for a relative address, or null for none.</returns>
        private static string Scheme(string url)
        {
            if (url is null)
            {
                return null;
            }

            var normalized = Normalize(url);

            for (var i = 0; i < normalized.Length; i++)
            {
                var c = normalized[i];

                if (c == ':')
                {
                    return i == 0 ? null : normalized[..i].ToLowerInvariant();
                }

                // a path, query or fragment before any colon makes the address relative
                if (c is '/' or '?' or '#')
                {
                    return "";
                }

                if (!char.IsAsciiLetterOrDigit(c) && c is not ('+' or '-' or '.'))
                {
                    // not a scheme character: no scheme the browser would act on, a relative path
                    return "";
                }
            }

            return "";
        }

        /// <summary>
        /// Removes what a browser ignores when it reads a scheme: whitespace and control characters.
        /// </summary>
        /// <param name="url">The address.</param>
        /// <returns>The address without them.</returns>
        private static string Normalize(string url)
        {
            var builder = new StringBuilder(url.Length);

            foreach (var c in url)
            {
                if (!char.IsWhiteSpace(c) && !char.IsControl(c))
                {
                    builder.Append(c);
                }
            }

            return builder.ToString();
        }
    }
}
