using System.Collections.Generic;
using WebExpress.WebCore.WebIcon;

namespace WebExpress.WebCore.WebUri
{
    /// <summary>
    /// One step of a breadcrumb that a <see cref="IUriPathSegmentTrail"/> contributes. A crumb
    /// may carry a menu, for instance the siblings of a page, so the trail is also a way sideways.
    /// </summary>
    public sealed class UriTrailCrumb
    {
        /// <summary>
        /// Gets the text, which may be an internationalization key.
        /// </summary>
        public string Text { get; init; }

        /// <summary>
        /// Gets the icon, or null.
        /// </summary>
        public IIcon Icon { get; init; }

        /// <summary>
        /// Gets the address the crumb links to, or null for a crumb that is text only.
        /// </summary>
        public IUri Uri { get; init; }

        /// <summary>
        /// Gets whether the crumb is left out of the breadcrumb, for a step that only exists
        /// in the address.
        /// </summary>
        public bool Hidden { get; init; }

        /// <summary>
        /// Gets the entries of the crumb's menu; empty for a crumb without one.
        /// </summary>
        public IEnumerable<UriTrailCrumb> Menu { get; init; } = [];
    }
}
