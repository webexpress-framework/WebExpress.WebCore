using WebExpress.WebCore.WebApplication;
using WebExpress.WebCore.WebIdentity;

namespace WebExpress.WebCore.Test
{
    /// <summary>
    /// A validator the test plugin defines, so discovery can be observed: it refuses only the
    /// account named <see cref="BlockedName"/>, which no other test signs in with.
    /// </summary>
    public sealed class TestIdentityValidator : IIdentityValidator
    {
        /// <summary>
        /// The name of the account the validator refuses.
        /// </summary>
        public const string BlockedName = "Mallory";

        /// <summary>
        /// Gets the application the validator was constructed for.
        /// </summary>
        public IApplicationContext ApplicationContext { get; }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="applicationContext">The application, injected by the discovery.</param>
        public TestIdentityValidator(IApplicationContext applicationContext)
        {
            ApplicationContext = applicationContext;
        }

        /// <summary>
        /// Refuses the blocked account.
        /// </summary>
        /// <param name="context">The identity to confirm.</param>
        /// <returns>False for the blocked account.</returns>
        public bool Validate(IdentityValidationContext context)
        {
            return context.Identity.Name != BlockedName;
        }
    }
}
