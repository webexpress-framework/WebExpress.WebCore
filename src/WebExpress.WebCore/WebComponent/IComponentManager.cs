using System;

namespace WebExpress.WebCore.WebComponent
{
    /// <summary>
    /// Common base type for the framework's managers. A manager is a central registry that keeps
    /// track of one kind of component (pages, resources, events, …) for the whole server and gives
    /// other parts of the system access to them. This base contract mainly ties their lifetime to
    /// <see cref="IDisposable"/>.
    /// </summary>
    public interface IComponentManager : IDisposable
    {
    }
}
