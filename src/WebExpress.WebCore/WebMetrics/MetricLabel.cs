namespace WebExpress.WebCore.WebMetrics
{
    /// <summary>
    /// Distinguishes series of one metric, such as successful from failed logins. Every distinct
    /// value creates a series of its own, so a label must only carry values from a small, bounded
    /// set and never user names, ids or paths.
    /// </summary>
    /// <param name="Name">The label name, matching <c>[a-zA-Z_][a-zA-Z0-9_]*</c>.</param>
    /// <param name="Value">The label value, which may be any text.</param>
    public readonly record struct MetricLabel(string Name, string Value);
}
