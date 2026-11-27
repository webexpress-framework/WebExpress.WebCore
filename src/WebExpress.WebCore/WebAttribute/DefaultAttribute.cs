using System;

namespace WebExpress.WebCore.WebAttribute
{
    /// <summary>
    /// Indicates that a status page is the default.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class DefaultAttribute : Attribute, IStatusPageAttribute
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public DefaultAttribute()
        {

        }
    }
}
