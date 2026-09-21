using System;
using System.Collections.Generic;
using WebExpress.WebCore.WebApplication;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebEndpoint;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebPage;

namespace WebExpress.WebCore.WebIdentity
{
    /// <summary>
    /// Interface for managing identities.
    /// </summary>
    public interface IIdentityManager : IComponentManager
    {
        /// <summary>
        /// Gets all permissions.
        /// </summary>
        IEnumerable<IIdentityPermissionContext> Permissions { get; }

        /// <summary>
        /// Gets all policies.
        /// </summary>
        IEnumerable<IIdentityPolicyContext> Policies { get; }

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
        /// An object that represents the response to the login dialog, including authentication results and any
        /// relevant status information. Returns <c>null</c> when no registered identity provider can handle the
        /// scenario; the server then falls back to the status page.
        /// </returns>
        IResponse CreateAuthenticationPrompt(IRequest request, IPageContext initiator, IIdentity identity = null);

        /// <summary>
        /// Creates a forbidden response page for the specified request when the authenticated
        /// user lacks the required permissions to access the requested resource.
        /// </summary>
        /// <param name="request">
        /// The request for which access was denied. Cannot be null.
        /// </param>
        /// <param name="initiator">
        /// The endpoint that the user attempted to access. Used to determine the origin and
        /// context of the authorization failure.
        /// </param>
        /// <param name="identity">
        /// The authenticated identity that lacks sufficient permissions. Cannot be null.
        /// </param>
        /// <returns>
        /// A response representing the forbidden page if a registered identity provider can handle the
        /// forbidden scenario; otherwise, <c>null</c>. The server then falls back to the status page.
        /// </returns>
        IResponse CreateForbiddenResponse(IRequest request, IPageContext initiator, IIdentity identity);

        /// <summary>
        /// Issues a provider-independent token pair and queues its protected response cookies.
        /// </summary>
        /// <param name="identity">The verified identity whose authorization snapshot is being processed.</param>
        /// <param name="request">The HTTP request whose authentication context is being evaluated.</param>
        /// <returns>The issued token pair, or null when no identity was authenticated.</returns>
        IdentityTokenPair Login(IIdentity identity, IRequest request);

        /// <summary>
        /// Clears authentication cookies and revokes renewal for the current login grant.
        /// </summary>
        /// <param name="request">The HTTP request whose authentication context is being evaluated.</param>
        void Logout(IRequest request);

        /// <summary>
        /// Rotates the refresh cookie at the dedicated endpoint without creating a session.
        /// </summary>
        /// <param name="request">The HTTP request whose authentication context is being evaluated.</param>
        /// <returns>The rotated token pair, or null when validation or replay protection rejects renewal.</returns>
        IdentityTokenPair Refresh(IRequest request);

        /// <summary>
        /// Issues a bounded bearer credential with a subset of the owner's permissions.
        /// </summary>
        /// <param name="identity">The verified identity whose authorization snapshot is being processed.</param>
        /// <param name="applicationContext">The application context that owns the requested operation.</param>
        /// <param name="lifetime">The explicit validity period requested for the personal credential.</param>
        /// <param name="permissions">The requested permission identifiers, limited to the owner's grants.</param>
        /// <returns>The signed personal credential with the requested authorized permissions.</returns>
        string CreatePersonalAccessToken(IIdentity identity, IApplicationContext applicationContext, TimeSpan lifetime, IEnumerable<string> permissions);

        /// <summary>
        /// Revokes a personal credential on every instance sharing the durable token store.
        /// </summary>
        /// <param name="token">The serialized credential that must pass the required trust checks.</param>
        /// <param name="applicationContext">The application context that owns the requested operation.</param>
        /// <returns>True when a valid personal credential was revoked; otherwise, false.</returns>
        bool RevokePersonalAccessToken(string token, IApplicationContext applicationContext);

        /// <summary>
        /// Delivers login, renewal, or logout cookie changes on the actual HTTP response.
        /// </summary>
        /// <param name="request">The HTTP request whose authentication context is being evaluated.</param>
        /// <param name="response">The outgoing response governed by the authentication transport contract.</param>
        void ApplyAuthenticationCookies(IRequest request, IResponse response);

        /// <summary>
        /// Returns the current signed-in identity based on the provided request.
        /// </summary>
        /// <param name="request">The request to get the current identity for.</param>
        /// <returns>The current signed-in identity.</returns>
        IIdentity GetCurrentIdentity(IRequest request);

        /// <summary>
        /// Checks whether the specified identity satisfies all policies associated with the given endpoint context.
        /// </summary>
        /// <param name="identity">The identity to check.</param>
        /// <param name="endpointContext">The endpoint context containing the policies to evaluate.</param>
        /// <returns>True if the identity has the permission, false otherwise.</returns>
        bool CheckAccess(IIdentity identity, IEndpointContext endpointContext);

        /// <summary>
        /// Checks whether the specified identity satisfies the given identity policy.
        /// </summary>
        /// <param name="identity">The identity to check.</param>
        /// <param name="policy">The identity policy to evaluate.</param>
        /// <returns>True if the identity is assigned to a group that contains the policy, false otherwise.</returns>
        bool CheckAccess(IIdentity identity, IIdentityPolicy policy);

        /// <summary>
        /// Checks whether the specified identity group satisfies the given identity policy.
        /// </summary>
        /// <param name="group">The identity group to check.</param>
        /// <param name="policy">The identity policy to evaluate.</param>
        /// <returns>True if the identity is assigned to a group that contains the policy, false otherwise.</returns>
        bool CheckAccess(IIdentityGroup group, IIdentityPolicy policy);

        /// <summary>
        /// Checks if the specified identity has the given permission.
        /// </summary>
        /// <typeparam name="TIdentityPermission">The type of the identity permission.</typeparam>
        /// <param name="applicationContext">The context of the application.</param>
        /// <param name="identity">The identity to check.</param>
        /// <returns>True if the identity has the permission, false otherwise.</returns>
        bool CheckAccess<TIdentityPermission>(IApplicationContext applicationContext, IIdentity identity)
            where TIdentityPermission : IIdentityPermission;

        /// <summary>
        /// Checks whether the given identity has the specified permission by evaluating all associated groups.
        /// </summary>
        /// <param name="applicationContext">The context of the application.</param>
        /// <param name="identity">The identity to check.</param>
        /// <param name="permission">The permission to check for.</param>
        /// <returns>True if any group grants the permission, false otherwise.</returns>
        bool CheckAccess(IApplicationContext applicationContext, IIdentity identity, Type permission);

        /// <summary>
        /// Checks if the specified identity group has the given permission.
        /// </summary>
        /// <typeparam name="TIdentityPermission">The type of the identity permission.</typeparam>
        /// <param name="applicationContext">The context of the application.</param>
        /// <param name="group">The identity group to check.</param>
        /// <returns>True if the identity group has the permission, false otherwise.</returns>
        bool CheckAccess<TIdentityPermission>(IApplicationContext applicationContext, IIdentityGroup group)
            where TIdentityPermission : IIdentityPermission;

        /// <summary>
        /// Checks if the specified identity group has the given permission.
        /// </summary>
        /// <param name="applicationContext">The context of the application.</param>
        /// <param name="group">The identity group to check.</param>
        /// <param name="permission">The permission to check for.</param>
        /// <returns>True if the identity group has the permission, false otherwise.</returns>
        bool CheckAccess(IApplicationContext applicationContext, IIdentityGroup group, Type permission);

        /// <summary>
        /// Checks if the specified identity policy has the given permission.
        /// </summary>
        /// <typeparam name="TIdentityPolicy">The type of the identity policy.</typeparam>
        /// <typeparam name="TIdentityPermission">The type of the identity permission.</typeparam>
        /// <param name="applicationContext">The context of the application.</param>
        /// <returns>True if the identity policy has the permission, false otherwise.</returns>
        bool CheckAccess<TIdentityPolicy, TIdentityPermission>(IApplicationContext applicationContext)
            where TIdentityPolicy : IIdentityPolicy
            where TIdentityPermission : IIdentityPermission;

        /// <summary>
        /// Checks if the specified identity policy has the given permission.
        /// </summary>
        /// <param name="applicationContext">The context of the application.</param>
        /// <param name="policy">The identity policy to check.</param>
        /// <param name="permission">The permission to check for.</param>
        /// <returns>True if the identity policy has the permission, false otherwise.</returns>
        bool CheckAccess(IApplicationContext applicationContext, Type policy, Type permission);

        /// <summary>
        /// Retrieves all available identities from the configured identity providers for the specified application
        /// context.
        /// </summary>
        /// <param name="applicationContext">
        /// The application context used to determine which identity providers to query. Cannot be null.
        /// </param>
        /// <returns>
        /// An enumerable collection of identities provided by all configured identity providers. The 
        /// collection is empty if no identities are available.
        /// </returns>
        IEnumerable<IIdentity> GetIdentities(IApplicationContext applicationContext);

        /// <summary>
        /// Retrieves all identity groups available from the configured group providers for the specified application
        /// context.
        /// </summary>
        /// <param name="applicationContext">
        /// The application context that determines which group providers are queried. Cannot be null.
        /// </param>
        /// <returns>
        /// An enumerable collection of identity groups available in the given application context. The 
        /// collection is empty if no groups are found.
        /// </returns>
        IEnumerable<IIdentityGroup> GetGroups(IApplicationContext applicationContext);
    }
}
