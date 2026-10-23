using WebExpress.WebCore.WebComponent;

namespace WebExpress.WebCore.WebApplication
{
    /// <summary>
    /// A self-contained application hosted by WebExpress — the top-level unit a plugin provides,
    /// under which pages, resources, and other components are grouped and mounted at a route.
    /// The framework calls <see cref="Run"/> once when the application starts.
    /// </summary>
    public interface IApplication : IComponent, System.IDisposable
    {
        /// <summary>
        /// Called when the application starts working. The call is concurrent.
        /// </summary>
        void Run();
    }
}
