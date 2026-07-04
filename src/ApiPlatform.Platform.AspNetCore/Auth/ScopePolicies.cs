using ApiPlatform.Platform.Auth;
using Microsoft.AspNetCore.Authorization;

namespace ApiPlatform.Platform.AspNetCore.Auth;

/// <summary>
/// Registers one AuthorizationPolicy per PlatformScopes value: each policy requires an
/// authenticated user possessing the matching "scope" claim.
/// </summary>
public static class ScopePolicies
{
    public static void AddPlatformScopePolicies(AuthorizationOptions opts)
    {
        foreach (var scope in new[]
        {
            PlatformScopes.AccountRead,
            PlatformScopes.AccountDetailedRead,
            PlatformScopes.TransactionRead,
            PlatformScopes.CustomerRead,
            PlatformScopes.ContactRead,
            PlatformScopes.EventPublish,
        })
        {
            opts.AddPolicy(scope, policy =>
                policy.RequireAuthenticatedUser()
                      .RequireClaim(PlatformScopes.ScopeClaimType, scope));
        }
    }
}
