using System.Threading;
using System.Threading.Tasks;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using WebExpress.WebCore.WebSetting;

namespace WebExpress.WebCore.WebEmail
{
    /// <summary>
    /// Opens an isolated SMTP connection per submission so concurrent applications cannot share protocol state.
    /// </summary>
    public sealed class SmtpEmailProvider : IEmailProvider
    {
        /// <summary>
        /// Gets the reserved provider name for built-in SMTP delivery.
        /// </summary>
        public string Name => "smtp";

        /// <summary>
        /// Submits MIME content over the explicitly selected TLS mode using normal certificate validation.
        /// </summary>
        /// <param name="message">The snapshot containing content and all envelope recipients.</param>
        /// <param name="profile">The validated connection settings and optional credentials.</param>
        /// <param name="cancellationToken">The caller cancellation and complete operation deadline.</param>
        /// <returns>A task that completes once the SMTP server accepts the message.</returns>
        public async Task SendAsync(MimeMessage message, EmailProfileSettings profile, CancellationToken cancellationToken)
        {
            using var client = new SmtpClient();
            var security = profile.Security switch
            {
                EmailSecurity.None => SecureSocketOptions.None,
                EmailSecurity.SslOnConnect => SecureSocketOptions.SslOnConnect,
                _ => SecureSocketOptions.StartTls
            };
            await client.ConnectAsync(profile.Host, profile.Port, security, cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(profile.UserName))
            {
                await client.AuthenticateAsync(profile.UserName, profile.Password, cancellationToken).ConfigureAwait(false);
            }

            await client.SendAsync(message, cancellationToken).ConfigureAwait(false);
            // disposing closes the connection without turning a failed quit into a failed delivery
        }
    }
}
