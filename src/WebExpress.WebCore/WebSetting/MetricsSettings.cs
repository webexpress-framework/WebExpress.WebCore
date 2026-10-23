namespace WebExpress.WebCore.WebSetting
{
    /// <summary>
    /// Optional settings of the global metrics endpoint. The whole block and every property in it is
    /// optional: a value left unset keeps the built-in default, and without the block the endpoint
    /// is not served.
    /// </summary>
    public sealed class MetricsSettings
    {
        /// <summary>
        /// Whether <c>/metrics</c> is served. Switched off, the path is left to normal application
        /// routing. Defaults to <c>false</c>: the series reveal load, login failures and the framework
        /// version, so a deployment publishes them only by an explicit decision.
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// A secret the scraper must send as <c>Authorization: Bearer &lt;token&gt;</c>. The series name
        /// no user, but they do reveal load, login failures and the framework version, so an endpoint
        /// reachable from outside the cluster should set one. Left unset, an enabled endpoint is open.
        /// </summary>
        public string BearerToken { get; set; }

        /// <summary>
        /// The period in minutes within which an authenticated request makes a user count as active.
        /// Values below one are raised to one.
        /// </summary>
        public int ActiveUserWindowMinutes { get; set; } = 5;
    }
}
