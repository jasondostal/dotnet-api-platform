using ApiPlatform.Platform.Auth;
using Microsoft.AspNetCore.Authorization;

namespace ApiPlatform.Api.Auth;

/// <summary>
/// Thin forwarding alias over PlatformScopes. Keeps existing endpoint references
/// compiling (they reference Scopes.AccountRead etc.) while the authoritative
/// definitions live in the platform core.
/// </summary>
public static class Scopes
{
    public const string AccountRead         = PlatformScopes.AccountRead;
    public const string AccountDetailedRead = PlatformScopes.AccountDetailedRead;
    public const string TransactionRead     = PlatformScopes.TransactionRead;
    public const string CustomerRead        = PlatformScopes.CustomerRead;
    public const string ContactRead         = PlatformScopes.ContactRead;
    public const string EventPublish        = PlatformScopes.EventPublish;

    /// <summary>
    /// Registers one AuthorizationPolicy per scope. Kept for backward compatibility;
    /// callers should prefer AddPlatformScopePolicies from ApiPlatform.Platform.AspNetCore.
    /// </summary>
    public static void AddScopePolicies(AuthorizationOptions opts)
    {
        foreach (var scope in new[] { AccountRead, AccountDetailedRead, TransactionRead, CustomerRead, ContactRead, EventPublish })
        {
            opts.AddPolicy(scope, policy =>
                policy.RequireAuthenticatedUser()
                      .RequireClaim(PlatformScopes.ScopeClaimType, scope));
        }
    }
}
