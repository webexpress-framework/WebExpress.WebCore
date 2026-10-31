using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WebExpress.WebCore.WebSetting;

namespace WebExpress.WebCore.WebEmail
{
    /// <summary>
    /// Isolates each SMTP submission while retaining explicit TLS requirements without external mail packages.
    /// </summary>
    public sealed class SmtpEmailProvider : IEmailProvider
    {
        /// <summary>Gets the reserved provider name for built-in SMTP delivery.</summary>
        public string Name => "smtp";

        /// <summary>
        /// Requires the selected TLS mode before sending credentials or message content.
        /// </summary>
        /// <param name="message">The snapshot containing content and all envelope recipients.</param>
        /// <param name="profile">The validated connection settings and optional credentials.</param>
        /// <param name="cancellationToken">The caller cancellation and complete operation deadline.</param>
        /// <returns>A task that completes once the SMTP server accepts the complete message.</returns>
        public async Task SendAsync(MailMessage message, EmailProfileSettings profile, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(message);
            ArgumentNullException.ThrowIfNull(profile);
            if (!Enum.IsDefined(profile.Security) ||
                (profile.Security == EmailSecurity.None && !string.IsNullOrEmpty(profile.UserName)))
            {
                throw new InvalidOperationException("SMTP authentication requires TLS.");
            }
            using var client = new TcpClient();
            await client.ConnectAsync(profile.Host, profile.Port, cancellationToken).ConfigureAwait(false);
            var local = ((System.Net.IPEndPoint)client.Client.LocalEndPoint).Address;
            var hello = "EHLO [" + (local.AddressFamily == AddressFamily.InterNetworkV6 ? "IPv6:" : "") + local + "]";
            Stream stream = client.GetStream();
            try
            {
                if (profile.Security == EmailSecurity.SslOnConnect)
                {
                    stream = await SecureAsync(stream, profile.Host, cancellationToken).ConfigureAwait(false);
                }
                await ReadReplyAsync(stream, cancellationToken, 220).ConfigureAwait(false);
                var capabilities = await CommandAsync(stream, hello, cancellationToken, 250).ConfigureAwait(false);
                if (profile.Security == EmailSecurity.StartTls)
                {
                    if (!HasCapability(capabilities, "STARTTLS"))
                    {
                        throw new SmtpException("The SMTP server does not advertise required STARTTLS.");
                    }
                    await CommandAsync(stream, "STARTTLS", cancellationToken, 220).ConfigureAwait(false);
                    stream = await SecureAsync(stream, profile.Host, cancellationToken).ConfigureAwait(false);
                    // pre-upgrade capabilities cannot authorize authentication on the protected connection
                    capabilities = await CommandAsync(stream, hello, cancellationToken, 250).ConfigureAwait(false);
                }
                await SubmitAsync(stream, message, profile, capabilities, cancellationToken).ConfigureAwait(false);
                // acceptance is final; connection cleanup must not turn an accepted message into a retry
            }
            finally
            {
                stream.Dispose();
            }
        }

        /// <summary>Uses platform certificate validation and the configured host for TLS identity checks.</summary>
        /// <param name="stream">The newly connected or upgraded SMTP stream.</param>
        /// <param name="host">The certificate identity expected by the deployment.</param>
        /// <param name="cancellationToken">The complete delivery deadline.</param>
        /// <returns>The authenticated TLS stream, owning its underlying transport.</returns>
        private static async Task<Stream> SecureAsync(Stream stream, string host, CancellationToken cancellationToken)
        {
            var secure = new SslStream(stream, false);
            try
            {
                await secure.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
                {
                    TargetHost = host,
                    CertificateRevocationCheckMode = System.Security.Cryptography.X509Certificates.X509RevocationMode.Online
                }, cancellationToken).ConfigureAwait(false);
                return secure;
            }
            catch
            {
                secure.Dispose();
                throw;
            }
        }

        /// <summary>Submits the envelope after required transport checks have succeeded.</summary>
        /// <param name="stream">The transport protected according to the selected profile.</param>
        /// <param name="message">The manager-owned MIME snapshot and envelope recipients.</param>
        /// <param name="profile">The credentials allowed on this transport.</param>
        /// <param name="capabilities">The latest EHLO response from the current transport.</param>
        /// <param name="cancellationToken">The complete delivery deadline.</param>
        /// <returns>A task completing after the final acceptance response.</returns>
        internal static async Task SubmitAsync(Stream stream, MailMessage message, EmailProfileSettings profile,
            IReadOnlyList<string> capabilities, CancellationToken cancellationToken)
        {
            if (!string.IsNullOrEmpty(profile.UserName))
            {
                var mechanisms = capabilities.Skip(1).Where(x => x.StartsWith("AUTH ", StringComparison.OrdinalIgnoreCase))
                    .SelectMany(x => x[5..].Split(' ', StringSplitOptions.RemoveEmptyEntries)).ToArray();
                if (mechanisms.Contains("PLAIN", StringComparer.OrdinalIgnoreCase))
                {
                    var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes("\0" + profile.UserName + "\0" + profile.Password));
                    await CommandAsync(stream, "AUTH PLAIN", cancellationToken, 334).ConfigureAwait(false);
                    await CommandAsync(stream, credentials, cancellationToken, 235).ConfigureAwait(false);
                }
                else if (mechanisms.Contains("LOGIN", StringComparer.OrdinalIgnoreCase))
                {
                    await CommandAsync(stream, "AUTH LOGIN", cancellationToken, 334).ConfigureAwait(false);
                    await CommandAsync(stream, Convert.ToBase64String(Encoding.UTF8.GetBytes(profile.UserName)), cancellationToken, 334).ConfigureAwait(false);
                    await CommandAsync(stream, Convert.ToBase64String(Encoding.UTF8.GetBytes(profile.Password)), cancellationToken, 235).ConfigureAwait(false);
                }
                else
                {
                    throw new SmtpException("The SMTP server does not advertise a supported authentication mechanism.");
                }
            }
            var sender = EnvelopeAddress(message.From);
            var recipients = message.To.Concat(message.CC).Concat(message.Bcc).Select(EnvelopeAddress).Distinct(StringComparer.Ordinal).ToArray();
            var international = new[] { sender }.Concat(recipients).Concat(message.ReplyToList.Select(EnvelopeAddress)).Any(x => !x.All(char.IsAscii));
            if (international && !HasCapability(capabilities, "SMTPUTF8"))
            {
                throw new SmtpException("The SMTP server does not support international mailbox names.");
            }
            await CommandAsync(stream, $"MAIL FROM:<{sender}>" + (international ? " SMTPUTF8" : ""), cancellationToken, 250).ConfigureAwait(false);
            foreach (var recipient in recipients)
            {
                await CommandAsync(stream, $"RCPT TO:<{recipient}>", cancellationToken, 250, 251).ConfigureAwait(false);
            }
            await CommandAsync(stream, "DATA", cancellationToken, 354).ConfigureAwait(false);
            await SmtpEmailWriter.WriteAsync(stream, message, cancellationToken).ConfigureAwait(false);
            await CommandAsync(stream, ".", cancellationToken, 250).ConfigureAwait(false);
        }

        /// <summary>Normalizes international domains without changing the mailbox local part.</summary>
        /// <param name="mailbox">The single mailbox validated by the manager.</param>
        /// <returns>An envelope address safe to embed in an SMTP path.</returns>
        internal static string EnvelopeAddress(MailAddress mailbox)
        {
            ArgumentNullException.ThrowIfNull(mailbox);
            var host = mailbox.Host.StartsWith('[') ? mailbox.Host : new IdnMapping().GetAscii(mailbox.Host);
            var address = mailbox.User + "@" + host;
            if (address.IndexOfAny(['\r', '\n', '\0', '<', '>']) >= 0 || Encoding.UTF8.GetByteCount(address) > 254)
            {
                throw new FormatException("The SMTP mailbox is invalid or too long.");
            }
            return address;
        }

        /// <summary>Separates EHLO extensions from the greeting and optional arguments.</summary>
        /// <param name="reply">The EHLO response without status prefixes.</param>
        /// <param name="name">The exact extension required by the selected operation.</param>
        /// <returns>True when the server advertises the extension.</returns>
        private static bool HasCapability(IReadOnlyList<string> reply, string name)
        {
            return reply.Skip(1).Any(x => x.Split(' ', 2)[0].Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>Keeps command framing under framework control and requires an expected status.</summary>
        /// <param name="stream">The connected SMTP transport.</param>
        /// <param name="command">One command or encoded authentication response without delimiters.</param>
        /// <param name="cancellationToken">The complete operation deadline.</param>
        /// <param name="expected">The status codes accepted for this protocol step.</param>
        /// <returns>The bounded response lines without status prefixes.</returns>
        private static async Task<IReadOnlyList<string>> CommandAsync(Stream stream, string command,
            CancellationToken cancellationToken, params int[] expected)
        {
            if (command.IndexOfAny(['\r', '\n', '\0']) >= 0) { throw new FormatException("Invalid SMTP command."); }
            await stream.WriteAsync(Encoding.UTF8.GetBytes(command + "\r\n"), cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            return await ReadReplyAsync(stream, cancellationToken, expected).ConfigureAwait(false);
        }

        /// <summary>Bounds untrusted responses and avoids buffering bytes across a TLS upgrade.</summary>
        /// <param name="stream">The current plaintext or TLS transport.</param>
        /// <param name="cancellationToken">The complete operation deadline.</param>
        /// <param name="expected">The status codes accepted for this protocol step.</param>
        /// <returns>The validated response lines without status prefixes.</returns>
        private static async Task<IReadOnlyList<string>> ReadReplyAsync(Stream stream, CancellationToken cancellationToken, params int[] expected)
        {
            var lines = new List<string>();
            var buffer = new byte[512];
            int? status = null;
            while (lines.Count < 100)
            {
                var length = 0;
                do
                {
                    if (length == buffer.Length || await stream.ReadAsync(buffer.AsMemory(length, 1), cancellationToken).ConfigureAwait(false) == 0)
                    {
                        throw new SmtpException("The SMTP response is incomplete or exceeds the line limit.");
                    }
                } while (buffer[length++] != '\n');
                var line = Encoding.ASCII.GetString(buffer, 0, length);
                if (length < 5 || !line.EndsWith("\r\n", StringComparison.Ordinal) ||
                    !int.TryParse(line.AsSpan(0, 3), NumberStyles.None, CultureInfo.InvariantCulture, out var code) ||
                    (status.HasValue && status != code) || (length > 5 && line[3] is not (' ' or '-')))
                {
                    throw new SmtpException("The SMTP response framing is invalid.");
                }
                status = code;
                lines.Add(length == 5 ? "" : line[4..^2]);
                if (length == 5 || line[3] == ' ')
                {
                    if (!expected.Contains(code)) { throw new SmtpException((SmtpStatusCode)code, "The SMTP server refused the operation."); }
                    return lines;
                }
            }
            throw new SmtpException("The SMTP response exceeds the multiline limit.");
        }
    }
}
