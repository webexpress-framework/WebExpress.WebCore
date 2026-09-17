namespace WebExpress.WebCore.WebSetting
{
    /// <summary>
    /// One address the web server listens on. An https endpoint additionally names the pfx file
    /// that holds its certificate, because Kestrel needs the certificate before the first
    /// connection can be accepted.
    /// </summary>
    public sealed class EndpointSettings
    {
        /// <summary>
        /// The uri to listen on, e.g. <c>http://localhost/</c> or <c>https://*:443/</c>. The host
        /// <c>*</c> stands for every address of the machine.
        /// </summary>
        public string Uri { get; set; }

        /// <summary>
        /// The certificate as a pfx file. Only needed for https.
        /// </summary>
        public string PfxFile { get; set; }

        /// <summary>
        /// The password that unlocks the pfx file.
        /// </summary>
        public string Password { get; set; }

        /// <summary>
        /// Conversion into its string representation.
        /// </summary>
        /// <returns>The uri of the endpoint.</returns>
        public override string ToString()
        {
            return Uri;
        }
    }
}
