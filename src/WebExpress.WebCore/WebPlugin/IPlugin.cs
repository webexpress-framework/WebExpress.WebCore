using WebExpress.WebCore.WebComponent;

namespace WebExpress.WebCore.WebPlugin
{
    /// <summary>
    /// A plugin — the deployable unit that extends WebExpress. A plugin packages applications and
    /// other components; the framework loads it, calls <see cref="Run"/> once at start-up, and can
    /// unload it again later.
    /// </summary>
    public interface IPlugin : IComponent, System.IDisposable
    {
        /// <summary>
        /// Called when the plugin starts working. The call is concurrent.
        /// </summary>
        void Run();
    }
}
