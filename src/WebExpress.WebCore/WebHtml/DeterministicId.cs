using System.Runtime.CompilerServices;
using System.Threading;

namespace WebExpress.WebCore.WebHtml
{
    /// <summary>
    /// Provides unique identifiers for HTML elements that need a stable handle within a rendered
    /// page, for example to wire a label to its input.
    /// </summary>
    /// <remarks>
    /// Uniqueness is guaranteed by a process-wide monotonic counter, which is all the generated
    /// markup requires: every control instance receives a distinct id within the page it renders on.
    /// An earlier implementation derived the id from the caller's source location and full call
    /// stack, which made every call walk the managed stack with per-frame reflection. That was
    /// roughly three orders of magnitude slower (~100 us instead of nanoseconds) and ran once per
    /// control construction, so it dominated server-side render time on control-heavy pages. It also
    /// produced duplicate ids for repeated calls from the same source line (e.g. inside a loop),
    /// because the call stack is identical across iterations.
    /// </remarks>
    public static class DeterministicId
    {
        private static long _counter;

        /// <summary>
        /// Returns a new identifier that is unique within the running process and is suitable as an
        /// HTML element id.
        /// </summary>
        /// <param name="context">
        /// An optional disambiguator. It no longer influences the result, because the counter
        /// already guarantees uniqueness; it is retained for source and binary compatibility.
        /// </param>
        /// <param name="file">
        /// Unused. Retained only for binary compatibility: the C# compiler bakes the
        /// <see cref="CallerFilePathAttribute"/> value into every existing call site, so removing
        /// the parameter would break already-compiled callers with a <c>MissingMethodException</c>.
        /// </param>
        /// <param name="line">
        /// Unused. Retained only for binary compatibility, see <paramref name="file"/>.
        /// </param>
        /// <returns>A unique identifier of the form <c>id_{hex}</c>.</returns>
        public static string Create
        (
            object context = null,
            [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0
        )
        {
            return "id_" + Interlocked.Increment(ref _counter).ToString("X");
        }
    }
}
