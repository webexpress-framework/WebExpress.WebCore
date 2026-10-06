using System;
using System.Linq;
using System.Text;

namespace WebExpress.WebCore.WebUri
{
    /// <summary>
    /// The authority (e.g. user@example.com:8080).
    /// </summary>
    public class UriAuthority
    {
        /// <summary>
        /// User information.
        /// </summary>
        public string User { get; set; }

        /// <summary>
        /// The password.
        /// is deprecated in RFC 3986 (section 3.2.1).
        /// </summary>
        [Obsolete("The property is deprecated (see RFC 3986 Section 3.2.1).", false)]
        public string Password { get; set; }

        /// <summary>
        /// The host (e.g. example.com, 192.0.2.16:80).
        /// </summary>
        public string Host { get; set; }

        /// <summary>
        /// The port (e.g. example.com, 192.0.2.16:80).
        /// </summary>
        public int? Port { get; set; }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public UriAuthority()
        {

        }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="host">The host.</param>
        public UriAuthority(string host)
        {
            Host = host;
        }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="host">The host.</param>
        /// <param name="port">The port.</param>
        public UriAuthority(string host, int port)
        {
            Host = host;
            Port = port;
        }

        /// <summary>
        /// Converts the authority to a string.
        /// </summary>
        /// <returns>The string representation of the authority.</returns>
        public override string ToString()
        {
            return ToString(-1);
        }

        /// <summary>
        /// Converts the authority to a string.
        /// </summary>
        /// <param name="defaultPort">The default port for the active scheme.</param>
        /// <returns>The string representation of the authority.</returns>
        public virtual string ToString(int defaultPort)
        {
            var builder = new StringBuilder();
            AppendTo(builder, defaultPort);

            return builder.ToString();
        }

        /// <summary>
        /// Appends the string representation of the authority directly to the given builder,
        /// avoiding the intermediate string/array allocations of <see cref="ToString(int)"/>.
        /// </summary>
        /// <param name="builder">The target builder.</param>
        /// <param name="defaultPort">The default port for the active scheme.</param>
        internal void AppendTo(StringBuilder builder, int defaultPort)
        {
            builder.Append("//");

#pragma warning disable 618
            var hasUser = !string.IsNullOrWhiteSpace(User);
            var hasPassword = !string.IsNullOrWhiteSpace(Password);
#pragma warning restore 618

            if (hasUser || hasPassword)
            {
                if (hasUser)
                {
                    builder.Append(User);
                }

                if (hasPassword)
                {
#pragma warning disable 618
                    if (hasUser)
                    {
                        builder.Append(':');
                    }

                    builder.Append(Password);
#pragma warning restore 618
                }

                builder.Append('@');
            }

            if (!string.IsNullOrWhiteSpace(Host))
            {
                builder.Append(Host);
            }

            if (Port.HasValue && Port != defaultPort)
            {
                builder.Append(':');
                builder.Append(Port.Value);
            }
        }
    }
}