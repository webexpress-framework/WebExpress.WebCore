using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebApplication;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebEndpoint;
using WebExpress.WebCore.WebIdentity.Model;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebPage;
using WebExpress.WebCore.WebPlugin;

namespace WebExpress.WebCore.WebIdentity
{
    /// <summary>
    /// Management of identities (users).
    /// </summary>
    public partial class IdentityManager : IIdentityManager
    {
        private readonly IComponentHub _componentHub;
        private readonly IHttpServerContext _httpServerContext;
        private readonly IdentityPermissionDictionary _permissionDictionary = [];
        private readonly IdentityPolicyDictionary _policyDictionary = [];

        /// <summary>
        /// Gets all permissions.
        /// </summary>
        public IEnumerable<IIdentityPermissionContext> Permissions => _permissionDictionary.Values
            .SelectMany(x => x.Values)
            .SelectMany(x => x)
            .Select(x => x.PermissionContext);

        /// <summary>
        /// Gets all policies.
        /// </summary>
        public IEnumerable<IIdentityPolicyContext> Policies => _policyDictionary.Values
            .SelectMany(x => x.Values)
            .SelectMany(x => x)
            .Select(x => x.PolicyContext);

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="componentHub">The component hub.</param>
        /// <param name="httpServerContext">The reference to the context of the host.</param>
        [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Used via Reflection.")]
        private IdentityManager(IComponentHub componentHub, IHttpServerContext httpServerContext)
        {
            _componentHub = componentHub;

            _componentHub?.PluginManager?.AddPlugin += OnAddPlugin;
            _componentHub?.PluginManager?.RemovePlugin += OnRemovePlugin;
            _componentHub?.ApplicationManager.AddApplication += OnAddApplication;
            _componentHub?.ApplicationManager.RemoveApplication += OnRemoveApplication;

            _httpServerContext = httpServerContext;

            _httpServerContext?.Log?.Debug
            (
                I18N.Translate
                (
                    "webexpress.webcore:identitymanager.initialization"
                )
            );
        }

        /// <summary>
        /// Discovers and binds jobs to an application.
        /// </summary>
        /// <param name="pluginContext">The context of the plugin whose jobs are to be associated.</param>
        private void Register(IPluginContext pluginContext)
        {
            if (_permissionDictionary.ContainsKey(pluginContext))
            {
                return;
            }

            Register(pluginContext, _componentHub?.ApplicationManager.GetApplications(pluginContext));
        }

        /// <summary>
        /// Discovers and binds jobs to an application.
        /// </summary>
        /// <param name="applicationContext">The context of the application whose jobs are to be associated.</param>
        private void Register(IApplicationContext applicationContext)
        {
            foreach (var pluginContext in _componentHub?.PluginManager?.GetPlugins(applicationContext))
            {
                if (_permissionDictionary.TryGetValue(pluginContext, out var appDict) && appDict.ContainsKey(applicationContext))
                {
                    continue;
                }

                Register(pluginContext, [applicationContext]);
            }
        }

        /// <summary>
        /// Registers policies and identities for a given plugin and application context.
        /// </summary>
        /// <param name="pluginContext">The plugin context.</param>
        /// <param name="applicationContexts">The application context (optional).</param>
        private void Register(IPluginContext pluginContext, IEnumerable<IApplicationContext> applicationContexts)
        {
            var assembly = pluginContext?.Assembly;

            // process permissions
            foreach (var permissionType in assembly.GetTypes().Where
                (
                    x => x.IsClass &&
                    x.IsSealed &&
                    x.IsPublic &&
                    (
                        x.GetInterface(typeof(IIdentityPermission).Name) is not null
                    )
                ))
            {
                var id = new ComponentId(permissionType.FullName);
                var policyTypes = new List<Type>();
                var matchingAttributes = permissionType.CustomAttributes
                    .Where
                    (
                        x => x.AttributeType.GetInterfaces().Contains(typeof(IPolicyAttribute)) &&
                        x.AttributeType.Name == typeof(PolicyAttribute<>).Name &&
                        x.AttributeType.Namespace == typeof(PolicyAttribute<>).Namespace
                    );

                foreach (var customAttribute in matchingAttributes)
                {
                    var type = customAttribute.AttributeType.GenericTypeArguments.FirstOrDefault();

                    if (type is not null && !policyTypes.Contains(type))
                    {
                        policyTypes.Add(type);
                    }
                }

                // assign the event to existing applications
                foreach (var applicationContext in applicationContexts)
                {
                    var permissionContext = new IdentityPermissionContext()
                    {
                        PluginContext = pluginContext,
                        ApplicationContext = applicationContext,
                        PermissionId = id,
                        Permission = permissionType
                    };

                    if (_permissionDictionary.AddPermissionItem
                    (
                        pluginContext,
                        applicationContext,
                        new IdentityPermissionItem(_componentHub, _httpServerContext, pluginContext, applicationContext, permissionType, permissionContext, policyTypes)
                    ))
                    {
                        _httpServerContext?.Log?.Debug
                        (
                            I18N.Translate
                            (
                                "webexpress.webcore:identitymanager.registerpermission",
                                id,
                                applicationContext.ApplicationId
                            )
                        );
                    }
                    else
                    {
                        _httpServerContext?.Log?.Debug
                        (
                            I18N.Translate
                            (
                                "webexpress.webcore:identitymanager.duplicatepermission",
                                id,
                                applicationContext.ApplicationId
                            )
                        );
                    }
                }
            }

            // process policies
            foreach (var policyType in assembly.GetTypes().Where
                (
                    x => x.IsClass &&
                    x.IsSealed &&
                    x.IsPublic &&
                    (
                        x.GetInterface(typeof(IIdentityPolicy).Name) is not null
                    )
                ))
            {
                var id = new ComponentId(policyType.FullName);
                var permissionTypes = new List<Type>();
                var matchingAttributes = policyType.CustomAttributes
                    .Where
                    (
                        x => x.AttributeType.GetInterfaces().Contains(typeof(IPermissionAttribute)) &&
                        x.AttributeType.Name == typeof(PermissionAttribute<>).Name &&
                        x.AttributeType.Namespace == typeof(PermissionAttribute<>).Namespace
                    );

                foreach (var customAttribute in matchingAttributes)
                {
                    var type = customAttribute.AttributeType.GenericTypeArguments.FirstOrDefault();

                    if (type is not null && !permissionTypes.Contains(type))
                    {
                        permissionTypes.Add(type);
                    }
                }

                // assign the event to existing applications
                foreach (var applicationContext in applicationContexts)
                {
                    var policyContext = new IdentityPolicyContext()
                    {
                        PluginContext = pluginContext,
                        ApplicationContext = applicationContext,
                        PolicyId = id,
                        Policy = policyType
                    };

                    if (_policyDictionary.AddPolicyItem
                    (
                        pluginContext,
                        applicationContext,
                        new IdentityPolicyItem(_componentHub, _httpServerContext, pluginContext, applicationContext, policyType, policyContext, permissionTypes)
                    ))
                    {
                        _httpServerContext?.Log?.Debug
                        (
                            I18N.Translate
                            (
                                "webexpress.webcore:identitymanager.registerpolicy",
                                id,
                                applicationContext.ApplicationId
                            )
                        );
                    }
                    else
                    {
                        _httpServerContext?.Log?.Debug
                        (
                            I18N.Translate
                            (
                                "webexpress.webcore:identitymanager.duplicatepolicy",
                                id,
                                applicationContext.ApplicationId
                            )
                        );
                    }
                }
            }
        }

        /// <summary>
        /// Removes all policies and permissions of an plugin.
        /// </summary>
        /// <param name="pluginContext">The context of the plugin that contains the identities to remove.</param>
        internal void Remove(IPluginContext pluginContext)
        {
            // remove permissions
            if (_permissionDictionary.TryGetValue(pluginContext, out var permissionValue))
            {
                foreach (var permissionItem in permissionValue.SelectMany(x => x.Value))
                {
                    permissionItem.Dispose();
                }

                _permissionDictionary.Remove(pluginContext);
            }

            // remove policies
            if (_policyDictionary.TryGetValue(pluginContext, out var policyValue))
            {
                foreach (var permissionItem in policyValue.SelectMany(x => x.Value))
                {
                    permissionItem.Dispose();
                }

                _policyDictionary.Remove(pluginContext);
            }
        }

        /// <summary>
        /// Removes all policies and permissions of an application.
        /// </summary>
        /// <param name="applicationContext">The context of the application that contains the identities to remove.</param>
        internal void Remove(IApplicationContext applicationContext)
        {
            if (applicationContext is null)
            {
                return;
            }

            // remove permissions
            foreach (var pluginDict in _permissionDictionary.Values)
            {
                foreach (var appDict in pluginDict.Where(x => x.Key == applicationContext).Select(x => x.Value))
                {
                    foreach (var permissionItem in appDict)
                    {
                        permissionItem.Dispose();
                    }
                }

                pluginDict.Remove(applicationContext);
            }

            // remove policies
            foreach (var pluginDict in _policyDictionary.Values)
            {
                foreach (var appDict in pluginDict.Where(x => x.Key == applicationContext).Select(x => x.Value))
                {
                    foreach (var policyItem in appDict)
                    {
                        policyItem.Dispose();
                    }
                }

                pluginDict.Remove(applicationContext);
            }
        }

        /// <summary>
        /// Raises the event when an plugin is added.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The context of the plugin being added.</param>
        private void OnAddPlugin(object sender, IPluginContext e)
        {
            Register(e);
        }

        /// <summary>  
        /// Raises the event when a plugin is removed.  
        /// </summary>  
        /// <param name="sender">The source of the event.</param>  
        /// <param name="e">The context of the plugin being removed.</param>  
        private void OnRemovePlugin(object sender, IPluginContext e)
        {
            Remove(e);
        }

        /// <summary>
        /// Raises the event when an application is removed.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The context of the application being removed.</param>
        private void OnRemoveApplication(object sender, IApplicationContext e)
        {
            Remove(e);
        }

        /// <summary>
        /// Raises the event when an application is added.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The context of the application being added.</param>
        private void OnAddApplication(object sender, IApplicationContext e)
        {
            Register(e);
        }

        /// <summary>
        /// Displays a login dialog using the specified request and identity information.
        /// </summary>
        /// <param name="request">
        /// The request containing parameters and context for the login operation. Cannot be null.
        /// </param>
        /// <param name="initiator">
        /// The endpoint that triggered the authentication process. Used to determine the origin and
        /// context of the authentication requirement.
        /// </param>
        /// <param name="identity">
        /// The identity information to be used for authentication. Cannot be null.
        /// </param>
        /// <returns>
        /// An object that represents the response to the login dialog, including authentication results 
        /// and any relevant status information.
        /// </returns>
        public IResponse CreateAuthenticationPrompt(IRequest request, IPageContext initiator, IIdentity identity = null)
        {
            if (initiator?.ApplicationContext is not null)
            {
                foreach (var provider in GetProviders(initiator.ApplicationContext))
                {
                    var response = provider.CreateAuthenticationPrompt(request, initiator, identity);

                    if (response is not null)
                    {
                        // the first provider that can show a login dialog wins
                        return response;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Creates a forbidden response page for the specified request when the authenticated
        /// user lacks the required permissions to access the requested resource.
        /// </summary>
        /// <param name="request">The request for which access was denied. Cannot be null.</param>
        /// <param name="initiator">The endpoint that the user attempted to access.</param>
        /// <param name="identity">The authenticated identity that lacks sufficient permissions.</param>
        /// <returns>
        /// A response representing the forbidden page if a registered identity provider can handle the 
        /// forbidden scenario; otherwise, <c>null</c>.
        /// </returns>
        public IResponse CreateForbiddenResponse(IRequest request, IPageContext initiator, IIdentity identity)
        {
            if (initiator?.ApplicationContext is not null)
            {
                foreach (var provider in GetProviders(initiator.ApplicationContext))
                {
                    var response = provider.CreateForbiddenResponse(request, initiator, identity);

                    if (response is not null)
                    {
                        // the first provider that can show a forbidden page wins
                        return response;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Checks whether the specified identity satisfies all policies associated with the given endpoint context.
        /// </summary>
        /// <param name="identity">The identity to check.</param>
        /// <param name="endpointContext">The endpoint context containing the policies to evaluate.</param>
        /// <returns>True if the identity has the permission, false otherwise.</returns>
        public bool CheckAccess(IIdentity identity, IEndpointContext endpointContext)
        {
            var policies = endpointContext.Policies ?? [];

            return policies.All(x => CheckAccess(identity, x));
        }

        /// <summary>
        /// Checks whether the specified identity satisfies the given identity policy.
        /// </summary>
        /// <param name="identity">The identity to check.</param>
        /// <param name="policy">The identity policy to evaluate.</param>
        /// <returns>True if the identity is assigned to a group that contains the policy, false otherwise.</returns>
        public bool CheckAccess(IIdentity identity, IIdentityPolicy policy)
        {
            if (identity is null || policy is null)
            {
                return false;
            }

            return policy is WebPolicies.AuthenticatedAccessPolicy || identity.PolicyNames.Contains(policy.GetType().FullName, StringComparer.Ordinal);
        }

        /// <summary>
        /// Checks whether the specified identity group satisfies the given identity policy.
        /// </summary>
        /// <param name="group">The identity group to check.</param>
        /// <param name="policy">The identity policy to evaluate.</param>
        /// <returns>True if the identity is assigned to a group that contains the policy, false otherwise.</returns>
        public bool CheckAccess(IIdentityGroup group, IIdentityPolicy policy)
        {
            if (group is null || policy is null)
            {
                return false;
            }

            // a group carries policy instances; the required policy matches by type
            return group.Policies?.Any(currentPolicy => currentPolicy?.GetType() == policy.GetType()) ?? false;
        }

        /// <summary>
        /// Checks if the specified identity has the given permission.
        /// </summary>
        /// <typeparam name="T">The type of the identity permission.</typeparam>
        /// <param name="applicationContext">The context of the application.</param>
        /// <param name="identity">The identity to check.</param>
        /// <returns>True if the identity has the permission, false otherwise.</returns>
        public bool CheckAccess<T>(IApplicationContext applicationContext, IIdentity identity)
            where T : IIdentityPermission
        {
            return CheckAccess(applicationContext, identity, typeof(T));
        }

        /// <summary>
        /// Checks whether the given identity has the specified permission by evaluating all associated groups,
        /// including the default "All" group to which every identity automatically belongs.
        /// </summary>
        /// <param name="applicationContext">The context of the application.</param>
        /// <param name="identity">The identity to check.</param>
        /// <param name="permission">The permission to check for.</param>
        /// <returns>True if any group grants the permission, false otherwise.</returns>
        public bool CheckAccess(IApplicationContext applicationContext, IIdentity identity, Type permission)
        {
            var groups = identity?.Groups ?? [];

            return identity?.Permissions.Contains(permission.FullName, StringComparer.Ordinal) == true ||
                groups.Any(group => CheckAccess(applicationContext, group, permission));
        }

        /// <summary>
        /// Checks if the specified identity group has the given permission.
        /// </summary>
        /// <typeparam name="T">The type of the identity permission.</typeparam>
        /// <param name="applicationContext">The context of the application.</param>
        /// <param name="group">The identity group to check.</param>
        /// <returns>True if the identity group has the permission, false otherwise.</returns>
        public bool CheckAccess<T>(IApplicationContext applicationContext, IIdentityGroup group)
            where T : IIdentityPermission
        {
            return CheckAccess(applicationContext, group, typeof(T));
        }

        /// <summary>
        /// Checks if the specified identity group has the given permission.
        /// </summary>
        /// <param name="group">The identity group to check.</param>
        /// <param name="applicationContext">The context of the application.</param>
        /// <param name="permission">The permission to check for.</param>
        /// <returns>True if the identity group has the permission, false otherwise.</returns>
        public bool CheckAccess(IApplicationContext applicationContext, IIdentityGroup group, Type permission)
        {
            return (group?.Policies ?? []).Any(policy => CheckAccess(applicationContext, policy.GetType(), permission));
        }

        /// <summary>
        /// Checks if the specified identity policy has the given permission.
        /// </summary>
        /// <typeparam name="R">The type of the identity policy.</typeparam>
        /// <typeparam name="P">The type of the identity permission.</typeparam>
        /// <param name="applicationContext">The context of the application.</param>
        /// <returns>True if the identity policy has the permission, false otherwise.</returns>
        public bool CheckAccess<R, P>(IApplicationContext applicationContext) where R : IIdentityPolicy
            where P : IIdentityPermission
        {
            return CheckAccess(applicationContext, typeof(R), typeof(P));
        }

        /// <summary>
        /// Checks if the specified identity policy has the given permission.
        /// </summary>
        /// <param name="applicationContext">The context of the application.</param>
        /// <param name="policyType">The identity policy to check.</param>
        /// <param name="permissionType">The permission to check for.</param>
        /// <returns>True if the identity policy has the permission, false otherwise.</returns>
        public bool CheckAccess(IApplicationContext applicationContext, Type policyType, Type permissionType)
        {
            return CheckAccess(applicationContext, policyType.FullName.ToLower(), permissionType);
        }

        /// <summary>
        /// Checks if the specified identity policy has the given permission.
        /// </summary>
        /// <param name="applicationContext">The context of the application.</param>
        /// <param name="policyName">The identity policy to check.</param>
        /// <param name="permissionType">The permission to check for.</param>
        /// <returns>True if the identity policy has the permission, false otherwise.</returns>
        private bool CheckAccess(IApplicationContext applicationContext, string policyName, Type permissionType)
        {
            // verify policies to permissions
            var policies = _policyDictionary.Values
                .SelectMany(x => x)
                .Where(x => x.Key == applicationContext)
                .SelectMany(entry => entry.Value);

            if (policies.Any(policy =>
                policy.PolicyClass.FullName.Equals(policyName, StringComparison.CurrentCultureIgnoreCase) &&
                policy.Permissions.Contains(permissionType)))
            {
                return true;
            }

            // verify permissions to policies
            var permissions = _permissionDictionary.Values
                .SelectMany(x => x)
                .Where(x => x.Key == applicationContext)
                .SelectMany(entry => entry.Value);

            if (permissions.Any(permission =>
                permission.PermissionClass == permissionType &&
                permission.Policies.Any(x => x.FullName.Equals(policyName, StringComparison.CurrentCultureIgnoreCase))))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Computes the SHA-256 hash of the input string.
        /// </summary>
        /// <param name="input">The input string to hash.</param>
        /// <returns>The computed hash as a hexadecimal string.</returns>
        public static string ComputeHash(SecureString input)
        {
            if (input is null)
            {
                return string.Empty;
            }

            var bstr = IntPtr.Zero;
            try
            {
                bstr = Marshal.SecureStringToBSTR(input);
                var length = Marshal.ReadInt32(bstr, -4);
                var bytes = new byte[length];

                // copy unmanaged string memory to a managed byte array
                Marshal.Copy(bstr, bytes, 0, length);

                // compute sha256 hash and convert to lower-case hex string
                var hashBytes = SHA256.HashData(bytes);

                return Convert.ToHexString(hashBytes).ToLowerInvariant();
            }
            finally
            {
                if (bstr != IntPtr.Zero)
                {
                    // safely free the unmanaged memory
                    Marshal.ZeroFreeBSTR(bstr);
                }
            }
        }

        /// <summary>
        /// Retrieves all available identities from the configured identity providers for the specified 
        /// application context.
        /// </summary>
        /// <param name="applicationContext">
        /// The application context used to determine which identity providers to query. Cannot be null.
        /// </param>
        /// <returns>
        /// An enumerable collection of identities provided by all configured identity providers. The 
        /// collection is empty if no identities are available.
        /// </returns>
        public IEnumerable<IIdentity> GetIdentities(IApplicationContext applicationContext)
        {
            return GetProviders(applicationContext)
                .SelectMany(p => p.GetIdentities());
        }

        /// <summary>
        /// Retrieves all identity groups available from the configured group providers for the specified 
        /// application context.
        /// </summary>
        /// <param name="applicationContext">
        /// The application context that determines which group providers are queried. Cannot be null.
        /// </param>
        /// <returns>
        /// An enumerable collection of identity groups available in the given application context. The collection 
        /// is empty if no groups are found.
        /// </returns>
        public IEnumerable<IIdentityGroup> GetGroups(IApplicationContext applicationContext)
        {
            return GetProviders(applicationContext)
                .SelectMany(p => p.GetGroups());
        }

        /// <summary>
        /// Retrieves the collection of identity providers associated with the specified application context.
        /// </summary>
        /// <param name="applicationContext">
        /// The application context for which to retrieve the identity providers. Cannot be null.
        /// </param>
        /// <returns>
        /// An enumerable collection of identity providers registered for the specified application context. Returns 
        /// an empty collection if no providers are found.
        /// </returns>
        private IEnumerable<IIdentityProvider> GetProviders(IApplicationContext applicationContext)
        {
            return _componentHub.IdentityProviderManager.GetProviders(applicationContext);
        }

        /// <summary>
        /// Release of unmanaged resources reserved during use.
        /// </summary>
        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }
}
