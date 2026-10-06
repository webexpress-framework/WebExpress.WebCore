using System.Threading;
using System.Threading.Tasks;
using MimeKit;
using WebExpress.WebCore.WebSetting;

namespace WebExpress.WebCore.WebEmail
{
    /// <summary>
    /// Allows plugins to connect external delivery APIs while retaining central validation and deduplication.
    /// Implementations must support concurrent calls, honor cancellation, and avoid internal retries.
    /// </summary>
    public interface IEmailProvider
    {
        /// <summary>
        /// Gets the unique configuration name used by mail profiles.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Submits one message and completes only after the provider accepts it.
        /// </summary>
        /// <param name="message">The manager-owned MIME snapshot, including envelope Bcc recipients.</param>
        /// <param name="profile">A private copy of the selected configuration.</param>
        /// <param name="cancellationToken">The caller cancellation and total delivery deadline.</param>
        /// <returns>A task that completes on acceptance or throws on failure.</returns>
        Task SendAsync(MimeMessage message, EmailProfileSettings profile, CancellationToken cancellationToken);
    }
}
