using System;

namespace WebExpress.WebCore.WebAttribute
{
    /// <summary>
    /// Applied to an endpoint class to mark its URI path segment as hidden, so it is left out of
    /// generated navigation such as menus and breadcrumbs while the endpoint itself stays reachable.
    /// </summary>
    /// <remarks>
    /// This attribute can be used to determine if the segment should not be displayed in user
    /// interfaces.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class SegmentHiddenAttribute : Attribute
    {

    }
}
