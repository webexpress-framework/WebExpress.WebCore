using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebInclude;

namespace WebExpress.WebCore.Test
{
    /// <summary>
    /// A dummy include whose files are embedded in the test plugin, so they are mounted and
    /// joined into a bundle per type.
    /// </summary>
    [Asset("/assets/js/myjavascript.js")]
    [Asset("/assets/css/mycss.css")]
    [Asset("/assets/js/myjavascript.mini.js")]
    [Asset("/assets/css/my-css.css")]
    public sealed class TestIncludeBundle : IInclude
    {
    }
}
