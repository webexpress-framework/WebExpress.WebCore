namespace WebExpress.WebCore.WebScope
{
    /// <summary>
    /// Marker type that names a "scope" — a contextual grouping that pages, setting pages, or
    /// includes can be tagged with via <c>[Scope&lt;TScope&gt;]</c>. Scopes let the framework decide
    /// where such elements apply (for example, limiting a fragment to a particular area of the app).
    /// </summary>
    public interface IScope
    {
    }
}
