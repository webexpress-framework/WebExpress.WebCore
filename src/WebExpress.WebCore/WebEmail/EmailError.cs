namespace WebExpress.WebCore.WebEmail
{
    /// <summary>
    /// Provides stable failure categories without requiring applications to interpret provider messages.
    /// </summary>
    public enum EmailError
    {
        /// <summary>Indicates that mail is disabled by the deployment.</summary>
        Disabled,
        /// <summary>Indicates that the requested profile or provider cannot be used.</summary>
        Configuration,
        /// <summary>Indicates that message validation failed before any delivery attempt.</summary>
        InvalidMessage,
        /// <summary>Indicates that shared duplicate protection could not be established.</summary>
        StoreUnavailable,
        /// <summary>Indicates a provider failure whose delivery outcome may be uncertain.</summary>
        DeliveryFailed,
        /// <summary>Indicates that the delivery budget expired and acceptance may be uncertain.</summary>
        Timeout,
        /// <summary>Indicates that the host no longer admits new deliveries.</summary>
        Stopping
    }
}
