using System.Threading;
using System.Threading.Tasks;
using WebExpress.WebCore.WebComponent;

namespace WebExpress.WebCore.WebEmail
{
    /// <summary>
    /// Gives every application the same validation, provider selection and cluster delivery policy.
    /// </summary>
    public interface IEmailManager : IComponentManager
    {
        /// <summary>
        /// Returns effective settings and current provider availability without exposing secrets.
        /// </summary>
        /// <returns>A detached diagnostic snapshot that performs no network operations.</returns>
        EmailStatus GetStatus();

        /// <summary>
        /// Registers a plugin-owned provider before applications start sending mail.
        /// </summary>
        /// <param name="provider">The thread-safe provider whose lifetime remains with the plugin.</param>
        void RegisterProvider(IEmailProvider provider);

        /// <summary>
        /// Removes a provider for future submissions when its plugin is unloaded.
        /// </summary>
        /// <param name="provider">The exact registered provider instance.</param>
        /// <returns>True when this instance was removed.</returns>
        bool UnregisterProvider(IEmailProvider provider);

        /// <summary>
        /// Attempts one logical delivery within the application's stable identifier namespace.
        /// </summary>
        /// <param name="applicationId">The stable application identifier shared by all replicas.</param>
        /// <param name="message">The message with a delivery identifier reused across replicas.</param>
        /// <param name="profile">The optional configured profile name.</param>
        /// <param name="cancellationToken">The caller's cancellation signal.</param>
        /// <returns>Provider acceptance or suppression of an existing attempt.</returns>
        Task<EmailSendResult> SendAsync(string applicationId, EmailMessage message, string profile = null,
            CancellationToken cancellationToken = default);
    }
}
