using System;
using System.Threading;

namespace WebExpress.WebCore.WebApplication.Model
{
    /// <summary>
    /// Represents an application entry in the application directory.
    /// </summary>
    internal class ApplicationItem
    {
        /// <summary>
        /// Gets the context associated with the application.
        /// </summary>
        public IApplicationContext ApplicationContext { get; set; }

        /// <summary>
        /// Gets the application class.
        /// </summary>
        public Type ApplicationClass { get; internal set; }

        /// <summary>
        /// Gets the application.
        /// </summary>
        public IApplication Application { get; set; }

        /// <summary>
        /// Gets the name the application declared through its <c>[Name]</c> attribute.
        /// </summary>
        /// <remarks>
        /// Kept beside the context so a runtime rename can be undone: the context carries what is
        /// shown now, this carries what the application asked to be called.
        /// </remarks>
        public string DeclaredApplicationName { get; set; }

        /// <summary>
        /// Gets the icon path the application declared through its <c>[Icon]</c> attribute,
        /// relative to the application. See <see cref="DeclaredApplicationName"/>.
        /// </summary>
        public string DeclaredIcon { get; set; }

        /// <summary>
        /// Gets the thread termination token.
        /// </summary>
        public CancellationTokenSource CancellationTokenSource { get; } = new CancellationTokenSource();
    }
}
