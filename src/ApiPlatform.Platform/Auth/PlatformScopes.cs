namespace ApiPlatform.Platform.Auth;

/// <summary>
/// Canonical scope constants for the platform. These are the authoritative definitions;
/// host-layer auth handlers reference these values.
/// </summary>
public static class PlatformScopes
{
    /// <summary>The claim type used for scope claims in all auth schemes.</summary>
    public const string ScopeClaimType = "scope";

    public const string AccountRead         = "account.read";
    public const string AccountDetailedRead = "account.detailed.read";
    public const string TransactionRead     = "transaction.read";
    public const string CustomerRead        = "customer.read";
    public const string ContactRead         = "contact.read";
    public const string EventPublish        = "event.publish";
}
