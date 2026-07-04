using System.Security.Claims;
using ApiPlatform.Platform.Auth;
using Microsoft.AspNetCore.Authentication;

namespace ApiPlatform.Platform.AspNetCore.Auth;

/// <summary>
/// Claims transformation for Entra/JWT Bearer mode. Splits the space-delimited scp (or
/// scope) claim into individual "scope" claims so the same scope-based authorization
/// policies work identically whether auth is header-based (dev) or JWT (prod).
/// </summary>
public sealed class EntraScopeClaims : IClaimsTransformation
{
    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        var identity = principal.Identity as ClaimsIdentity;
        if (identity is null)
            return Task.FromResult(principal);

        // Only needed when a single combined scp/scope claim contains multiple values
        var raw = principal.FindFirstValue("scp") ?? principal.FindFirstValue("scope");
        if (raw is null || !raw.Contains(' '))
            return Task.FromResult(principal);

        // Clone to avoid mutating the incoming principal
        var cloned = principal.Clone();
        var clonedIdentity = cloned.Identity as ClaimsIdentity;
        if (clonedIdentity is null)
            return Task.FromResult(principal);

        // Remove the combined claim and replace with individual scope claims
        var combined = clonedIdentity.FindFirst(c => c.Type is "scp" or "scope");
        if (combined is not null)
            clonedIdentity.RemoveClaim(combined);

        foreach (var scope in raw.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            clonedIdentity.AddClaim(new Claim(PlatformScopes.ScopeClaimType, scope));

        return Task.FromResult(cloned);
    }
}
