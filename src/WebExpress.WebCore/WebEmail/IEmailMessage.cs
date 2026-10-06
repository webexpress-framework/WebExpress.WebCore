using System.Collections.Generic;

namespace WebExpress.WebCore.WebEmail
{
    /// <summary>
    /// Describes a logical delivery independently of the selected server or provider.
    /// </summary>
    public interface IEmailMessage
    {
        /// <summary>
        /// Gets the stable business delivery identifier that callers must reuse across nodes and retries.
        /// </summary>
        public string DeliveryId { get; }

        /// <summary>
        /// Gets the optional sender override, falling back to the selected profile.
        /// </summary>
        public string From { get; }

        /// <summary>
        /// Gets the primary recipients as individual mailbox addresses.
        /// </summary>
        public IReadOnlyList<string> To { get; }

        /// <summary>
        /// Gets the visible copy recipients as individual mailbox addresses.
        /// </summary>
        public IReadOnlyList<string> Cc { get; }

        /// <summary>
        /// Gets the envelope-only recipients that must not appear in delivered headers.
        /// </summary>
        public IReadOnlyList<string> Bcc { get; }

        /// <summary>
        /// Gets the optional mailbox to receive replies instead of the sender.
        /// </summary>
        public string ReplyTo { get; }

        /// <summary>
        /// Gets the subject without allowing additional header lines.
        /// </summary>
        public string Subject { get; }

        /// <summary>
        /// Gets the plain text body or the alternative to the HTML body.
        /// </summary>
        public string TextBody { get; }

        /// <summary>
        /// Gets the HTML body supplied and escaped as necessary by the application.
        /// </summary>
        public string HtmlBody { get; }

        /// <summary>
        /// Gets the attachments copied before asynchronous submission begins.
        /// </summary>
        public IReadOnlyList<EmailAttachment> Attachments { get; }
    }
}
