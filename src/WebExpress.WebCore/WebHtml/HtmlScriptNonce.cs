using System;
using System.Threading;

namespace WebExpress.WebCore.WebHtml
{
    /// <summary>
    /// Carries the content security policy nonce of the response that is currently being
    /// rendered to the inline scripts in it. The html tree is built long before the response
    /// exists and is shared across requests, so the nonce cannot be stored on the elements;
    /// it is scoped to the serialization instead.
    /// </summary>
    public static class HtmlScriptNonce
    {
        private static readonly AsyncLocal<string> _current = new();

        /// <summary>
        /// Gets the nonce of the rendering in progress, or null outside of one.
        /// </summary>
        public static string Current => _current.Value;

        /// <summary>
        /// Makes the nonce visible to every inline script serialized until the scope is disposed.
        /// </summary>
        /// <param name="nonce">The nonce announced in the content security policy header.</param>
        /// <returns>A scope that restores the previous nonce when disposed.</returns>
        public static IDisposable Begin(string nonce)
        {
            var previous = _current.Value;
            _current.Value = nonce;

            return new Scope(previous);
        }

        /// <summary>
        /// Restores the nonce that was active before the scope began.
        /// </summary>
        private sealed class Scope(string previous) : IDisposable
        {
            /// <summary>
            /// Restores the previous nonce.
            /// </summary>
            public void Dispose()
            {
                _current.Value = previous;
            }
        }
    }
}
