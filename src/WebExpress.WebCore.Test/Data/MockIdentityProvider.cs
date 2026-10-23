using WebExpress.WebCore.WebIdentity;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebPage;

namespace WebExpress.WebCore.Test.Data
{
    /// <summary>
    /// Provides a mock implementation of the IIdentityProvider interface for testing purposes.
    /// </summary>
    /// <remarks>
    /// This class simulates an identity provider by allowing test code to manage in-memory
    /// collections of identities and groups. It is intended for use in unit tests or development scenarios where a real
    /// identity provider is not required.
    /// </remarks>
    public class MockIdentityProvider : IIdentityProvider
    {
        /// <summary>
        /// Gets the collection of identities associated with the current principal.
        /// </summary>
        public List<IIdentity> Identities { get; } = [];

        /// <summary>
        /// Gets the collection of identity groups associated with the current user or entity.
        /// </summary>
        public List<IIdentityGroup> Groups { get; } = [];

        /// <summary>
        /// Returns a collection of all associated identities for the current principal.
        /// </summary>
        /// <returns>
        /// An enumerable collection of <see cref="IIdentity"/> objects representing the identities associated with the
        /// principal. The collection may be empty if no identities are present.
        /// </returns>
        public IEnumerable<IIdentity> GetIdentities() => Identities;

        /// <summary>
        /// Retrieves a collection of identity groups associated with the current context.
        /// </summary>
        /// <returns>
        /// An enumerable collection of objects that implement the IIdentityGroup interface. The collection may be empty
        /// if no groups are associated.
        /// </returns>
        public IEnumerable<IIdentityGroup> GetGroups() => Groups;

        /// <summary>
        /// Authenticates the specified request and returns the associated identity.
        /// </summary>
        /// <param name="request">
        /// The request to authenticate. Cannot be null.
        /// </param>
        /// <returns>
        /// An identity representing the authenticated user if authentication is successful; otherwise, null.
        /// </returns>
        public IIdentity Authenticate(IRequest request)
        {
            return null; // not needed for this test
        }

        /// <summary>
        /// Logs out the specified request by clearing any authentication state.
        /// </summary>
        /// <param name="request">
        /// The request whose authentication state should be cleared. Cannot be null.
        /// </param>
        public void Logout(IRequest request)
        {
            // not needed for this test
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
        /// An object that represents the response to the login dialog, including authentication results and any
        /// relevant status information.
        /// </returns>
        public IResponse CreateAuthenticationPrompt(IRequest request, IPageContext initiator, IIdentity identity)
        {
            return null;
        }

        /// <summary>
        /// Creates a forbidden response page for the specified request when the authenticated
        /// user lacks the required permissions to access the requested resource.
        /// </summary>
        /// <param name="request">
        /// The request for which access was denied. Cannot be null.
        /// </param>
        /// <param name="initiator">
        /// The endpoint that the user attempted to access.
        /// </param>
        /// <param name="identity">
        /// The authenticated identity that lacks sufficient permissions.
        /// </param>
        /// <returns>
        /// A response representing the forbidden page if this provider can handle the forbidden
        /// scenario; otherwise, <c>null</c>.
        /// </returns>
        public IResponse CreateForbiddenResponse(IRequest request, IPageContext initiator, IIdentity identity)
        {
            return null;
        }
    }
}
