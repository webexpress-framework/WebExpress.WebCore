using System;
using WebExpress.WebCore.WebUri;

namespace WebExpress.WebCore.WebMessage
{
    /// <summary>
    /// Represents an exception that is thrown to redirect a web page.
    /// </summary>
    public class RedirectException : Exception
    {
        /// <summary>
        /// Gets or sets the redirection target.
        /// </summary>
        public IUri Uri { get; set; }

        /// <summary>
        /// Determines whether a permanent redirection should occur.
        /// </summary>
        public bool Permanent { get; set; }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="uri">The redirection target.</param> 
        /// <param name="permanent">true if 301 should be sent, false for 302.</param>
        public RedirectException(IUri uri, bool permanent = false)
            : base("Redirecting to " + uri)
        {
            Uri = uri;
            Permanent = permanent;
        }
    }
}
