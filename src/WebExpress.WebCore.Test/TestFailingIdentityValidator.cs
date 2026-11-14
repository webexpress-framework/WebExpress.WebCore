using WebExpress.WebCore.WebIdentity;

namespace WebExpress.WebCore.Test
{
    /// <summary>
    /// A validator of the test plugin whose construction fails while a test has armed it. The
    /// switch flows with the test's own execution context, so hubs other tests boot in parallel
    /// construct it normally and it accepts every identity there.
    /// </summary>
    public sealed class TestFailingIdentityValidator : IIdentityValidator
    {
        private static readonly AsyncLocal<bool> _armed = new();

        /// <summary>
        /// Gets or sets whether the construction fails in the current execution context.
        /// </summary>
        public static bool Armed
        {
            get => _armed.Value;
            set => _armed.Value = value;
        }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <exception cref="InvalidOperationException">The test armed the failure.</exception>
        public TestFailingIdentityValidator()
        {
            if (Armed)
            {
                throw new InvalidOperationException("directory not configured");
            }
        }

        /// <summary>
        /// Accepts the identity.
        /// </summary>
        /// <param name="context">The identity to confirm.</param>
        /// <returns>Always true.</returns>
        public bool Validate(IdentityValidationContext context)
        {
            return true;
        }
    }
}
