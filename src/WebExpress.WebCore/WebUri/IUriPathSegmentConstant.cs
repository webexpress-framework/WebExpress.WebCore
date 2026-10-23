namespace WebExpress.WebCore.WebUri
{
    /// <summary>
    /// A single part of a URI path (the pieces between the slashes) that is a fixed literal, such as
    /// <c>ix</c> in <c>/ix/home</c>. It matches a request only when that part of the path is exactly equal.
    /// </summary>
    public interface IUriPathSegmentConstant : IUriPathSegment
    {
    }
}