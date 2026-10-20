using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace WebExpress.WebCore.WebHtml
{
    /// <summary>
    /// Provides unique identifiers for HTML elements that need a stable handle within a rendered
    /// page, for example to wire a label to its input.
    /// </summary>
    /// <remarks>
    /// Within a scope - the server opens one around every request - the ids are counted from the
    /// start of that scope, so a page built in the same order yields the same ids on every request.
    /// That is what a form without an explicit id relies on: its name derives from its id, and the
    /// request that submits it has to rebuild the very name the request that rendered it handed out,
    /// or the submit is never recognized and silently dropped.
    /// <para>
    /// Outside a scope - during startup, on a background worker, in a test - a process-wide counter
    /// is used. Its ids carry a prefix of their own, because a control built there may be rendered
    /// inside a request, next to the ids that request counts.
    /// </para>
    /// <para>
    /// An earlier implementation derived the id from the caller's source location and full call
    /// stack, which made every call walk the managed stack with per-frame reflection. That was
    /// roughly three orders of magnitude slower (~100 us instead of nanoseconds) and ran once per
    /// control construction, so it dominated server-side render time on control-heavy pages. It also
    /// produced duplicate ids for repeated calls from the same source line (e.g. inside a loop),
    /// because the call stack is identical across iterations.
    /// </para>
    /// </remarks>
    public static class DeterministicId
    {
        private static long _counter;
        private static readonly AsyncLocal<Scope> _scope = new();

        /// <summary>
        /// Starts counting the ids anew until the returned scope is closed.
        /// </summary>
        /// <remarks>
        /// The server opens a scope around every request. A host that renders pages outside of a
        /// request and needs the ids to repeat - a test, a pre-renderer - opens one itself. Scopes
        /// nest: closing one restores the count of the enclosing scope.
        /// </remarks>
        /// <returns>The scope. Closing it restores the scope that was current before.</returns>
        public static IDisposable BeginScope()
        {
            var scope = new Scope(_scope.Value);

            _scope.Value = scope;

            return scope;
        }

        /// <summary>
        /// Returns a new identifier that is unique within the current scope - or within the
        /// running process outside of one - and is suitable as an HTML element id.
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
        /// <returns>
        /// A unique identifier of the form <c>id_{hex}</c> within a scope and <c>id_g{hex}</c>
        /// outside of one.
        /// </returns>
        public static string Create
        (
            object context = null,
            [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0
        )
        {
            var scope = _scope.Value;

            if (scope is not null)
            {
                return "id_" + scope.Next().ToString("X");
            }

            return "id_g" + Interlocked.Increment(ref _counter).ToString("X");
        }

        /// <summary>
        /// The count of one scope opened by <see cref="BeginScope"/>.
        /// </summary>
        /// <param name="previous">The scope that was current when this one was opened.</param>
        private sealed class Scope(Scope previous) : IDisposable
        {
            private long _counter;
            private bool _closed;

            /// <summary>
            /// Returns the next number of the scope.
            /// </summary>
            /// <returns>The next number, starting at one.</returns>
            public long Next()
            {
                return Interlocked.Increment(ref _counter);
            }

            /// <summary>
            /// Restores the scope that was current when this one was opened.
            /// </summary>
            public void Dispose()
            {
                if (_closed)
                {
                    return;
                }

                _closed = true;
                _scope.Value = previous;
            }
        }
    }
}
