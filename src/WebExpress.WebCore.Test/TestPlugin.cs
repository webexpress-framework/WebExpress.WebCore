using WebExpress.WebCore.WebApplication;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebPlugin;

namespace WebExpress.WebCore.Test
{
    /// <summary>
    /// A dummy plugin for testing purposes.
    /// </summary>
    [Name("TestPlugin")]
    [Description("plugin.description")]
    [Icon("/assets/img/Logo.png")]
    [Application<TestApplicationA>()]
    [Application<TestApplicationB>()]
    [Application<IApplication>()]
    public sealed class TestPlugin : IPlugin
    {
        /// <summary>
        /// Determines whether the plugin has been released, so a test can tell that removing
        /// the plugin disposes this very instance.
        /// </summary>
        public bool IsDisposed { get; private set; }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="pluginContext">The plugin context.</param>
        private TestPlugin(IPluginContext pluginContext)
        {

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
