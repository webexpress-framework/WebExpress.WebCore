using System;
using System.Collections.Generic;
using WebExpress.WebCore.WebApplication;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebPlugin;

namespace WebExpress.WebCore.WebResource
{
    /// <summary>
    /// The resource manager manages resources elements, which can be called with a URI (Uniform Resource Identifier).
    /// </summary>
    public interface IResourceManager : IComponentManager
    {
        /// <summary>
        /// An event that fires when an resource is added.
        /// </summary>
        event EventHandler<IResourceContext> AddResource;

        /// <summary>
        /// An event that fires when an resource is removed.
        /// </summary>
        event EventHandler<IResourceContext> RemoveResource;

        /// <summary>
        /// Gets all resource contexts.
        /// </summary>
        IEnumerable<IResourceContext> Resources { get; }

        /// <summary>
        /// Returns an enumeration of all containing resource contexts of a plugin.
        /// </summary>
        /// <param name="pluginContext">A context of a plugin whose resources are to be registered.</param>
        /// <returns>An enumeration of resource contexts.</returns>
        IEnumerable<IResourceContext> GetResources(IPluginContext pluginContext);

        /// <summary>
        /// Returns an enumeration of resource contextes.
        /// </summary>
        /// <typeparam name="T">The resource type.</typeparam>
        /// <returns>An enumeration of resource contextes.</returns>
        IEnumerable<IResourceContext> GetResources<T>() where T : IResource;

        /// <summary>
        /// Returns an enumeration of resource contextes.
        /// </summary>
        /// <param name="resourceType">The resource type.</param>
        /// <returns>An enumeration of resource contextes.</returns>
        IEnumerable<IResourceContext> GetResources(Type resourceType);

        /// <summary>
        /// Returns an enumeration of resource contextes.
        /// </summary>
        /// <param name="resourceType">The resource type.</param>
        /// <param name="applicationContext">The context of the application.</param>
        /// <returns>An enumeration of resource contextes.</returns>
        IEnumerable<IResourceContext> GetResources(Type resourceType, IApplicationContext applicationContext);

        /// <summary>
        /// Returns an enumeration of resource contextes.
        /// </summary>
        /// <typeparam name="T">The resource type.</typeparam>
        /// <param name="applicationContext">The context of the application.</param>
        /// <returns>An enumeration of resource contextes.</returns>
        IEnumerable<IResourceContext> GetResources<T>(IApplicationContext applicationContext) where T : IResource;

        /// <summary>
        /// Returns the resource context.
        /// </summary>
        /// <param name="applicationContext">The context of the application.</param>
        /// <param name="resourceId">The resource id.</param>
        /// <returns>An resource context or null.</returns>
        IResourceContext GetResource(IApplicationContext applicationContext, string resourceId);

        /// <summary>
        /// Returns the resource context.
        /// </summary>
        /// <param name="applicationId">The application id.</param>
        /// <param name="resourceId">The resource id.</param>
        /// <returns>An resource context or null.</returns>
        IResourceContext GetResource(string applicationId, string resourceId);
    }
}
