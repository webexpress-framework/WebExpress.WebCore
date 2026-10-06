namespace WebExpress.WebCore.WebPlugin.Model
{
    /// <summary>
    /// Tells whether a plugin is currently usable. A plugin only becomes <c>Active</c> once all the
    /// other plugins it depends on are present; until then it stays in <c>WaitingForDependencies</c>.
    /// </summary>
    public enum PluginRuntimeState
    {
        /// <summary>
        /// The plugin is loaded and active.
        /// </summary>
        Active,

        /// <summary>
        /// The plugin is known but waiting for one or more dependencies.
        /// </summary>
        WaitingForDependencies
    }
}
