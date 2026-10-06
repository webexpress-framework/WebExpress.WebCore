using System;

namespace WebExpress.WebCore
{
    /// <summary>
    /// Represents the running web server that hosts WebExpress. It is the top-level entry point
    /// that owns the server-wide context (<see cref="IHttpServerContext"/>) and signals, via the
    /// <see cref="Started"/> event, when the server is ready to accept requests.
    /// </summary>
    public interface IHost
    {
        /// <summary>
        /// Gets the context of the host.
        /// </summary>
        IHttpServerContext HttpServerContext { get; }

        /// <summary>
        /// Event is triggered after the web server starts.
        /// </summary>
        event EventHandler Started;
    }
}
