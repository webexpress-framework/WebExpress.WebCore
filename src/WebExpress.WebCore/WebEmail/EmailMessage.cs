using System;
using System.Collections.Generic;

namespace WebExpress.WebCore.WebEmail
{
    /// <summary>
    /// Describes a logical delivery independently of the selected server or provider.
    /// </summary>
    public sealed class EmailMessage : IEmailMessage
    {
        /// <summary>
        /// Gets the stable business delivery identifier that callers must reuse across nodes and retries.
        /// </summary>
        public string DeliveryId { get; init; } = Guid.NewGuid().ToString("N");

        /// <summary>
        /// Gets the optional sender override, falling back to the selected profile.
        /// </summary>
        public string From { get; init; }

        /// <summary>
        /// Gets the primary recipients as individual mailbox addresses.
        /// </summary>
        public IReadOnlyList<string> To { get; init; } = [];

        /// <summary>
        /// Gets the visible copy recipients as individual mailbox addresses.
        /// </summary>
        public IReadOnlyList<string> Cc { get; init; } = [];

        /// <summary>
        /// Gets the envelope-only recipients that must not appear in delivered headers.
        /// </summary>
        public IReadOnlyList<string> Bcc { get; init; } = [];

        /// <summary>
        /// Gets the optional mailbox to receive replies instead of the sender.
        /// </summary>
        public string ReplyTo { get; init; }

        /// <summary>
        /// Gets the subject without allowing additional header lines.
        /// </summary>
        public string Subject { get; init; } = "";

        /// <summary>
        /// Gets the plain text body or the alternative to the HTML body.
        /// </summary>
        public string TextBody { get; init; }

        /// <summary>
        /// Gets the HTML body supplied and escaped as necessary by the application.
        /// </summary>
        public string HtmlBody { get; init; }

        /// <summary>
        /// Gets the attachments copied before asynchronous submission begins.
        /// </summary>
        public IReadOnlyList<EmailAttachment> Attachments { get; init; } = [];
    }
}
