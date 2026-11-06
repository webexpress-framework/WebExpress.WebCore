using System.Collections.Generic;
using WebExpress.WebCore.WebPage;

namespace WebExpress.WebCore.WebUri
{
    /// <summary>
    /// A path segment that stands for more than one step of the trail. The segment of a page in
    /// a hierarchy - a wiki page, an org unit - is a single segment of the address, but the
    /// visitor expects to see every ancestor in the breadcrumb, each a link of its own. A
    /// breadcrumb renders the crumbs of such a segment in place of the one the segment would give.
    /// </summary>
    /// <remarks>
    /// The segment is reached through a segment attribute whose <c>ToPathSegment</c> returns it;
    /// a variable segment is copied and bound before the trail is asked for, so
    /// <see cref="IUriPathSegment.Value"/> holds the value of the current request.
    /// </remarks>
    public interface IUriPathSegmentTrail : IUriPathSegment
    {
        /// <summary>
        /// Returns the crumbs the segment contributes, from the outermost to the innermost.
        /// </summary>
        /// <param name="renderContext">The context of the page the breadcrumb is rendered on.</param>
        /// <returns>The crumbs; a hidden crumb is skipped.</returns>
        IEnumerable<UriTrailCrumb> GetTrail(IRenderContext renderContext);
    }
}
