using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WebExpress.WebCore.WebEmail
{
    /// <summary>
    /// Serializes framework-owned messages as MIME without persisting message content to disk.
    /// </summary>
    internal static class SmtpEmailWriter
    {
        /// <summary>Omits Bcc headers and encodes content so SMTP terminators cannot occur in body data.</summary>
        /// <param name="stream">The SMTP DATA transport, owned by the caller.</param>
        /// <param name="message">The validated snapshot containing alternatives and copied attachments.</param>
        /// <param name="cancellationToken">The complete submission deadline.</param>
        /// <returns>A task completing when the MIME body is flushed, before SMTP acceptance.</returns>
        internal static async Task WriteAsync(Stream stream, MailMessage message, CancellationToken cancellationToken)
        {
            // disposing after a cancelled flush would synchronously retry buffered network writes
            var writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true) { NewLine = "\r\n" };
            await LineAsync(writer, "Date: " + DateTimeOffset.UtcNow.ToString("ddd, dd MMM yyyy HH:mm:ss +0000", CultureInfo.InvariantCulture), cancellationToken);
            await LineAsync(writer, "Message-ID: " + message.Headers["Message-ID"], cancellationToken);
            await AddressesAsync(writer, "From", [message.From], cancellationToken);
            await AddressesAsync(writer, "To", message.To, cancellationToken);
            await AddressesAsync(writer, "Cc", message.CC, cancellationToken);
            await AddressesAsync(writer, "Reply-To", message.ReplyToList, cancellationToken);
            await LineAsync(writer, "Subject: " + EncodeHeader(message.Subject), cancellationToken);
            await LineAsync(writer, "MIME-Version: 1.0", cancellationToken);
            var mixed = "wx-mixed-" + Guid.NewGuid().ToString("N");
            if (message.Attachments.Count > 0)
            {
                await LineAsync(writer, $"Content-Type: multipart/mixed; boundary=\"{mixed}\"\r\n", cancellationToken);
                await LineAsync(writer, "--" + mixed, cancellationToken);
            }
            await BodyAsync(writer, message, cancellationToken);
            foreach (var attachment in message.Attachments)
            {
                await LineAsync(writer, "--" + mixed, cancellationToken);
                var contentType = new System.Net.Mime.ContentType(attachment.ContentType.ToString());
                contentType.Parameters.Remove("name");
                await LineAsync(writer, "Content-Type: " + contentType, cancellationToken);
                await LineAsync(writer, "Content-Transfer-Encoding: base64", cancellationToken);
                // extended parameters keep unicode filenames outside quoted ascii header syntax
                var filename = string.Concat(Encoding.UTF8.GetBytes(attachment.Name).Select(x => "%" + x.ToString("X2", CultureInfo.InvariantCulture)));
                await LineAsync(writer, "Content-Disposition: attachment;", cancellationToken);
                for (var offset = 0; offset < filename.Length; offset += 45)
                {
                    var part = filename.Substring(offset, Math.Min(45, filename.Length - offset));
                    var suffix = offset + 45 < filename.Length ? ";" : "";
                    await LineAsync(writer, $" filename*{offset / 45}*=" + (offset == 0 ? "utf-8''" : "") + part + suffix, cancellationToken);
                }
                await LineAsync(writer, "", cancellationToken);
                await ContentAsync(writer, attachment.ContentStream, cancellationToken);
            }
            if (message.Attachments.Count > 0) { await LineAsync(writer, "--" + mixed + "--", cancellationToken); }
            await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
            writer.Dispose();
        }

        /// <summary>Preserves alternative ordering so clients can select HTML while retaining plain text.</summary>
        /// <param name="writer">The MIME output with CRLF line separators.</param>
        /// <param name="message">The manager-owned alternatives.</param>
        /// <param name="cancellationToken">The complete submission deadline.</param>
        /// <returns>A task completing after all alternatives have been serialized.</returns>
        private static async Task BodyAsync(StreamWriter writer, MailMessage message, CancellationToken cancellationToken)
        {
            var boundary = "wx-alternative-" + Guid.NewGuid().ToString("N");
            if (message.AlternateViews.Count > 1)
            {
                await LineAsync(writer, $"Content-Type: multipart/alternative; boundary=\"{boundary}\"\r\n", cancellationToken);
            }
            foreach (var view in message.AlternateViews)
            {
                if (message.AlternateViews.Count > 1) { await LineAsync(writer, "--" + boundary, cancellationToken); }
                await LineAsync(writer, "Content-Type: " + view.ContentType.MediaType + "; charset=utf-8", cancellationToken);
                await LineAsync(writer, "Content-Transfer-Encoding: base64\r\n", cancellationToken);
                await ContentAsync(writer, view.ContentStream, cancellationToken);
            }
            if (message.AlternateViews.Count > 1) { await LineAsync(writer, "--" + boundary + "--", cancellationToken); }
        }

        /// <summary>Folds visible mailbox lists without emitting envelope-only Bcc recipients.</summary>
        /// <param name="writer">The MIME output writer.</param>
        /// <param name="name">The framework-controlled header name.</param>
        /// <param name="addresses">The validated mailbox collection.</param>
        /// <param name="cancellationToken">The complete submission deadline.</param>
        /// <returns>A task completing after the optional address header.</returns>
        private static async Task AddressesAsync(StreamWriter writer, string name,
            System.Collections.Generic.IEnumerable<MailAddress> addresses, CancellationToken cancellationToken)
        {
            var values = addresses.Select(address => (string.IsNullOrEmpty(address.DisplayName) ? "" : EncodeHeader(address.DisplayName) + "\r\n ")
                + "<" + SmtpEmailProvider.EnvelopeAddress(address) + ">").ToArray();
            if (values.Length > 0) { await LineAsync(writer, name + ": " + string.Join(",\r\n ", values), cancellationToken); }
        }

        /// <summary>Keeps encoded words below MIME limits without splitting UTF-8 characters.</summary>
        /// <param name="value">The validated subject or display name.</param>
        /// <returns>A folded sequence of UTF-8 base64 encoded words.</returns>
        private static string EncodeHeader(string value)
        {
            var words = new System.Collections.Generic.List<string>();
            var part = new StringBuilder();
            var size = 0;
            foreach (var rune in value.EnumerateRunes())
            {
                if (size + rune.Utf8SequenceLength > 42)
                {
                    words.Add("=?utf-8?B?" + Convert.ToBase64String(Encoding.UTF8.GetBytes(part.ToString())) + "?=");
                    part.Clear();
                    size = 0;
                }
                part.Append(rune.ToString());
                size += rune.Utf8SequenceLength;
            }
            if (part.Length > 0) { words.Add("=?utf-8?B?" + Convert.ToBase64String(Encoding.UTF8.GetBytes(part.ToString())) + "?="); }
            return string.Join("\r\n ", words);
        }

        /// <summary>Streams binary content in 76-character base64 lines without dot-stuffing ambiguity.</summary>
        /// <param name="writer">The MIME output writer.</param>
        /// <param name="content">The copied content stream positioned at its beginning.</param>
        /// <param name="cancellationToken">The complete submission deadline.</param>
        /// <returns>A task completing when the content stream has been consumed.</returns>
        private static async Task ContentAsync(StreamWriter writer, Stream content, CancellationToken cancellationToken)
        {
            var buffer = new byte[57];
            int count;
            while ((count = await content.ReadAtLeastAsync(buffer, buffer.Length, false, cancellationToken).ConfigureAwait(false)) > 0)
            {
                await LineAsync(writer, Convert.ToBase64String(buffer, 0, count), cancellationToken);
            }
        }

        /// <summary>Applies cancellation to buffered MIME writes as well as network operations.</summary>
        /// <param name="writer">The MIME output writer.</param>
        /// <param name="value">The framework-framed header or encoded content line.</param>
        /// <param name="cancellationToken">The complete submission deadline.</param>
        /// <returns>A task completing after the line is buffered or written.</returns>
        private static Task LineAsync(StreamWriter writer, string value, CancellationToken cancellationToken)
        {
            return writer.WriteLineAsync(value.AsMemory(), cancellationToken);
        }
    }
}
