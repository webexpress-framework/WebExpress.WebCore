namespace WebExpress.WebCore.WebEmail
{
    /// <summary>
    /// Carries attachment bytes without transferring ownership of application streams or files.
    /// </summary>
    public interface IEmailAttachment
    {
        /// <summary>
        /// Gets the display filename without local directory information.
        /// </summary>
        public string FileName { get; }

        /// <summary>
        /// Gets the MIME media type used by mail clients to interpret the attachment.
        /// </summary>
        public string ContentType { get; }

        /// <summary>
        /// Gets the application-owned bytes copied by the manager before delivery.
        /// </summary>
        public byte[] Content { get; }
    }
}
