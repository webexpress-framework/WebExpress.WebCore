using System.Collections.Generic;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebPage;

namespace WebExpress.WebCore.WebIdentity
{
    /// <summary>
    /// Represents an external identity provider that supplies identities and groups
    /// to the WebExpress identity system.
    /// </summary>
    public interface IIdentityProvider
    {
        /// <summary>
        /// Returns all identities provided by this source.
        /// </summary>
        IEnumerable<IIdentity> GetIdentities();

        /// <summary>
        /// Returns all groups provided by this source.
        /// </summary>
        IEnumerable<IIdentityGroup> GetGroups();

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
        /// relevant status information. A provider that cannot handle the scenario returns <c>null</c>; the
        /// identity manager then asks the next provider, and the server falls back to the status page.
        /// </returns>
        IResponse CreateAuthenticationPrompt(IRequest request, IPageContext initiator, IIdentity identity);

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
        /// A response representing the forbidden page if this provider can handle the forbidden
        /// scenario; otherwise, <c>null</c>. When every provider returns <c>null</c>, the identity
        /// manager reports no response and the server falls back to the status page.
        /// </returns>
        IResponse CreateForbiddenResponse(IRequest request, IPageContext initiator, IIdentity identity);
    }
}
