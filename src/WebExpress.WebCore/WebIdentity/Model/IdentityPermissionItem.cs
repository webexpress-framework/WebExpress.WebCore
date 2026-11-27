using System;
using System.Collections.Generic;
using WebExpress.WebCore.WebApplication;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebPlugin;

namespace WebExpress.WebCore.WebIdentity.Model
{
    /// <summary>
    /// Represents an identity permission item used in the web identity system.
    /// </summary>
    public class IdentityPermissionItem
    {
        private readonly IComponentHub _componentHub;

        /// <summary>
        /// Gets the associated plugin context.
        /// </summary>
        public IPluginContext PluginContext { get; private set; }

        /// <summary>
        /// Gets the corresponding application context.
        /// </summary>
        public IApplicationContext ApplicationContext { get; private set; }

        /// <summary>
        /// Gets the policies associated with the permission.
        /// </summary>
        public IEnumerable<Type> Policies { get; private set; }

        /// <summary>
        /// Gets the permission context.
        /// </summary>
        public IIdentityPermissionContext PermissionContext { get; private set; }

        /// <summary>
        /// Gets the permission class.
        /// </summary>
        public Type PermissionClass { get; private set; }

        /// <summary>
        /// Gets the permission instance.
        /// </summary>
        public IIdentityPermission Instance { get; }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="componentHub">The associated component hub.</param>
        /// <param name="httpServerContext">The reference to the context of the host.</param>
        /// <param name="pluginContext">The associated plugin context.</param>
        /// <param name="applicationContext">The corresponding application context.</param>
        /// <param name="permissionClass">The permission class.</param>
        /// <param name="permissionContext">The permission context.</param>
        /// <param name="policies">The policies associated with the permission.</param>
        public IdentityPermissionItem(IComponentHub componentHub, IHttpServerContext httpServerContext, IPluginContext pluginContext, IApplicationContext applicationContext, Type permissionClass, IIdentityPermissionContext permissionContext, IEnumerable<Type> policies)
        {
            _componentHub = componentHub;
            PluginContext = pluginContext;
            ApplicationContext = applicationContext;
            Policies = policies;
            PermissionClass = permissionClass;
            PermissionContext = permissionContext;
            Instance = ComponentActivator.CreateInstance<IIdentityPermission>(httpServerContext, _componentHub, PermissionClass, PermissionContext);
        }

        /// <summary>
        /// Performs application-specific tasks related to sharing, returning, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            if (Instance is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        /// <summary>
        /// Convert the resource element to a string.
        /// </summary>
        /// <returns>The event element in its string representation.</returns>
        public override string ToString()
        {
            return $"Permission: '{PermissionClass.FullName.ToLowerInvariant()}'";
        }
    }
}
