namespace WebExpress.WebCore.WebHealt
{
    /// <summary>
    /// Separates a dependency's health decision from diagnostics reserved for the server log.
    /// </summary>
    public sealed class HealthCheckResult
    {
        /// <summary>
        /// Gets whether the dependency can currently support application requests.
        /// </summary>
        public bool IsHealthy { get; }

        /// <summary>
        /// Gets diagnostic context that must never be included in a public health response.
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// Keeps health results immutable while several probes await the same check.
        /// </summary>
        /// <param name="isHealthy">Whether the dependency is available.</param>
        /// <param name="description">The diagnostic context reserved for server logs.</param>
        private HealthCheckResult(bool isHealthy, string description)
        {
            IsHealthy = isHealthy;
            Description = description;
        }

        /// <summary>
        /// Confirms that the dependency can support application requests.
        /// </summary>
        /// <returns>A successful check result.</returns>
        public static HealthCheckResult Healthy()
        {
            return new HealthCheckResult(true, null);
        }

        /// <summary>
        /// Rejects traffic while retaining diagnostic context for the operator.
        /// </summary>
        /// <param name="description">The failure detail reserved for server logs.</param>
        /// <returns>An unsuccessful check result.</returns>
        public static HealthCheckResult Unhealthy(string description = null)
        {
            return new HealthCheckResult(false, description);
        }
    }
}
