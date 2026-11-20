using System;
using System.Text.RegularExpressions;

namespace WebExpress.WebCore.WebMessage
{
    /// <summary>
    /// Represents an authorization request containing type, identification, password, token, and raw data.
    /// </summary>
    public partial class RequestAuthorization
    {
        /// <summary>
        /// Returns a regular expression to match the authorization header.
        /// </summary>
        /// <returns>A <see cref="Regex"/> object for matching authorization headers.</returns>
        [GeneratedRegex("^(.*?) (.*)$")]
        private static partial Regex AuthorizationRegex();

        /// <summary>
        /// Gets or sets the authorization type (e.g., Basic, Bearer, ApiKey, Digest, Custom).
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// Gets or sets the login name for basic auth.
        /// </summary>
        public string Identification { get; set; }

        /// <summary>
        /// Gets or sets the password for basic auth.
        /// </summary>
        public string Password { get; set; }

        /// <summary>
        /// Gets or sets the token for bearer or apikey auth.
        /// </summary>
        public string Token { get; set; }

        /// <summary>
        /// Gets or sets the raw data for digest or custom auth.
        /// </summary>
        public string RawData { get; set; }

        /// <summary>
        /// Parses the authorization request string into an object.
        /// </summary>
        /// <param name="str">The authorization string to parse.</param>
        /// <returns>A parsed <see cref="RequestAuthorization"/> instance or null if the input is null.</returns>
        public static RequestAuthorization Parse(string str)
        {
            if (str is null)
            {
                return null;
            }

            var m = AuthorizationRegex().Match(str);
            var auth = new RequestAuthorization();

            if (m.Success && m.Groups.Count >= 3)
            {
                auth.Type = m.Groups[1].Value;
                var data = m.Groups[2].Value;

                // store the raw data for digest or custom implementations
                auth.RawData = data;

                // parse based on the extracted authorization type
                var typeLower = auth.Type.ToLowerInvariant();

                if (typeLower == "basic")
                {
                    try
                    {
                        // decode the base64 string
                        var decoded = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(data));
                        // only the user id is barred from containing a colon (RFC 7617), the
                        // password may hold any number of them
                        var split = decoded.Split(':', 2);

                        if (split.Length > 0)
                        {
                            auth.Identification = split[0];
                        }

                        if (split.Length > 1)
                        {
                            auth.Password = split[1];
                        }
                    }
                    catch
                    {
                        // ignore parsing errors to gracefully fall back
                    }
                }
                else if (typeLower == "bearer" || typeLower == "apikey")
                {
                    // assign the raw data to the token property
                    auth.Token = data;
                }
            }

            return auth;
        }
    }
}