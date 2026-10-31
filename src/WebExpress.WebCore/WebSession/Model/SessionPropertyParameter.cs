using System.Collections.Generic;
using WebExpress.WebCore.WebParameter;

namespace WebExpress.WebCore.WebSession.Model
{
    /// <summary>
    /// A session property that remembers request parameters for the user's session. Parameters
    /// stored here are re-applied to later requests (as session-scoped parameters), so values can
    /// persist across page calls without being resent each time.
    /// </summary>
    public class SessionPropertyParameter : SessionProperty
    {
        /// <summary>
        /// Gets the parameters.
        /// </summary>
        public Dictionary<string, Parameter> Params { get; } = [];

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public SessionPropertyParameter()
        {
        }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="parameters">The parameters.</param>
        public SessionPropertyParameter(params Parameter[] parameters)
        {
            foreach (var param in parameters)
            {
                Params.Add(param.Key, param);
            }
        }
    }
}
