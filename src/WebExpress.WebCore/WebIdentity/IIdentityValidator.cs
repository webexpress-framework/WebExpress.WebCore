namespace WebExpress.WebCore.WebIdentity
{
    /// <summary>
    /// Confirms on every request that a signed identity may still act. A signed access token is a
    /// snapshot that stays valid until it expires; a validator closes that window for what the
    /// application knows better than the token - an account that was disabled, a grant that was
    /// withdrawn - without waiting for the next renewal.
    /// </summary>
    /// <remarks>
    /// A plugin's public sealed class implementing the interface is discovered for every
    /// application the plugin serves and constructed once per application; its constructor may
    /// take the component hub, the server context, the application context and the plugin
    /// context. A validator runs on every authenticated request, so it answers from a cache or
    /// an index rather than from a slow store. A validator that throws refuses the identity.
    /// </remarks>
    public interface IIdentityValidator
    {
        /// <summary>
        /// Decides whether the identity of a request is still accepted.
        /// </summary>
        /// <param name="context">The identity, its token's grant and the request.</param>
        /// <returns>True to accept the identity, false to treat the request as anonymous.</returns>
        bool Validate(IdentityValidationContext context);
    }
}
