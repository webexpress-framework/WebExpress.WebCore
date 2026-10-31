using WebExpress.WebCore.WebApplication;
using WebExpress.WebCore.WebAttribute;

namespace WebExpress.WebCore.Test
{
    /// <summary>
    /// A dummy application for testing purposes.
    /// </summary>
    [Name("TestApplicationA")]
    [Description("application.description")]
    [Icon("/assets/img/Logo.png")]
    [ContextPath("/appa")]
    [AssetPath("/asseta")]
    [DataPath("/dataa")]
    [Dependency("webexpress.webui")]
    [Theme<TestThemeA>]
    public sealed class TestApplicationA : IApplication
    {
        /// <summary>
        /// Determines whether the application has been released, so a test can tell that
        /// removing its plugin disposes this very instance.
        /// </summary>
        public bool IsDisposed { get; private set; }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="applicationContext">The application context, for testing the injection.</param>
        private TestApplicationA(IApplicationContext applicationContext)
        {
            // test the injection
            if (applicationContext is null)
            {
                throw new ArgumentNullException(nameof(applicationContext), "Parameter cannot be null or empty.");
            }
        }

        /// <summary>
        /// Called when the plugin starts working. The call is concurrent.
        /// </summary>
        public void Run()
        {
        }

        /// <summary>
        /// Release of unmanaged resources reserved during use.
        /// </summary>
        public void Dispose()
        {
            IsDisposed = true;
        }
    }
}
