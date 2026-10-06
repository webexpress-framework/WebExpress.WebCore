using System;
using System.Collections.Generic;
using WebExpress.WebCore.WebApplication;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebPlugin;

namespace WebExpress.WebCore.WebIdentity.Model
{
    /// <summary>
    /// Internal record the identity system keeps for one registered policy. It links the policy
    /// class and its created instance to the plugin and application it belongs to, and lists the
    /// permissions the policy requires.
    /// </summary>
    public class IdentityPolicyItem
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
        /// Gets the policy context.
        /// </summary>
        public IIdentityPolicyContext PolicyContext { get; private set; }

        /// <summary>
        /// Gets the permissions associated with the policy.
        /// </summary>
        public IEnumerable<Type> Permissions { get; private set; }

        /// <summary>
        /// Gets the policy class.
        /// </summary>
        public Type PolicyClass { get; private set; }

        /// <summary>
        /// Gets the policy instance.
        /// </summary>
        public IIdentityPolicy Instance { get; }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="componentHub">The associated component hub.</param>
        /// <param name="httpServerContext">The reference to the context of the host.</param>
        /// <param name="pluginContext">The associated plugin context.</param>
        /// <param name="applicationContext">The corresponding application context.</param>
        /// <param name="permissionClass">The policy class.</param>
        /// <param name="policyContext">The policy context.</param>
        /// <param name="permissions">The permissions associated with the policy.</param>
        public IdentityPolicyItem(IComponentHub componentHub, IHttpServerContext httpServerContext, IPluginContext pluginContext, IApplicationContext applicationContext, Type permissionClass, IIdentityPolicyContext policyContext, IEnumerable<Type> permissions)
        {
            _componentHub = componentHub;
            PluginContext = pluginContext;
            ApplicationContext = applicationContext;
            Permissions = permissions;
            PolicyClass = permissionClass;
            PolicyContext = policyContext;
            Instance = ComponentActivator.CreateInstance<IIdentityPolicy>(httpServerContext, _componentHub, PolicyClass, PolicyContext);
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
            return $"Policy: '{PolicyClass.FullName.ToLower()}'";
        }
    }
}
