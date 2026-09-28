namespace WebExpress.WebCore.WebSetting
{
    /// <summary>
    /// Controls the shared entry point without changing registered application routes.
    /// </summary>
    public sealed class RootSettings
    {
        /// <summary>
        /// Gets or sets whether the entry point may redirect. Disabling redirects always shows
        /// the application overview, even when an application has been selected explicitly.
        /// </summary>
        public bool RedirectEnabled { get; set; } = true;

        /// <summary>
        /// Gets or sets the registered application identifier selected for redirects. An omitted
        /// identifier selects the only application automatically. An unknown identifier shows the overview.
        /// </summary>
        public string ApplicationId { get; set; }
    }
}
