using System;
using WebExpress.WebCore.WebPlugin;

namespace WebExpress.WebCore.WebApplication
{
    /// <summary>
    /// Records a declared application whose constructor threw, so the failure outlives the log line.
    /// </summary>
    /// <remarks>
    /// An application that cannot be created is never registered: it has no context, no routes and
    /// no health bindings. Without this record the host would carry on as if the plugin declared
    /// nothing, and every probe would report a host that serves only 404s as healthy.
    /// </remarks>
    public sealed class ApplicationFailure
    {
        /// <summary>
        /// Gets the id the application would have been registered with.
        /// </summary>
        public string ApplicationId { get; init; }

        /// <summary>
        /// Gets the plugin that declares the application; its removal discards the record.
        /// </summary>
        public IPluginContext PluginContext { get; init; }

        /// <summary>
        /// Gets the exception thrown by the constructor, unwrapped from the reflection call.
        /// </summary>
        public Exception Exception { get; init; }
    }
}
