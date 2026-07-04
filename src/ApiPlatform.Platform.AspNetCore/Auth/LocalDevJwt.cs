using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace ApiPlatform.Platform.AspNetCore.Auth;

/// <summary>
/// Helper for <c>AUTH_MODE=LocalJwt</c>: mints signed HS256 JWTs for local development
/// and offline tests. No cloud tenant or external identity provider is required.
/// </summary>
public static class LocalDevJwt
{
    /// <summary>Default JWT issuer for LocalJwt mode. Overridable via <c>AUTH_ISSUER</c>.</summary>
    public const string DefaultIssuer = "api-platform-local";

    /// <summary>Default JWT audience for LocalJwt mode. Overridable via <c>AUTH_AUDIENCE</c>.</summary>
    public const string DefaultAudience = "api-platform-local";

    /// <summary>
    /// NON-SECRET dev-only signing key committed in source. Must NEVER be used outside a
    /// local development environment. Override via the <c>AUTH_SIGNING_KEY</c> configuration
    /// value before deploying to any shared or production environment.
    /// </summary>
    public const string DefaultDevKey = "local-dev-signing-key-change-me-0123456789ABCDEF";

    /// <summary>
    /// Mints a signed HS256 JWT suitable for <c>AUTH_MODE=LocalJwt</c>.
    /// </summary>
    /// <param name="key">Signing key (UTF-8 string; must be ≥ 32 bytes for HS256).</param>
    /// <param name="issuer">Value of the <c>iss</c> claim.</param>
    /// <param name="audience">Value of the <c>aud</c> claim.</param>
    /// <param name="scopes">
    ///   Scopes to embed as a space-delimited <c>scope</c> claim. Multiple scopes are
    ///   expanded by <see cref="EntraScopeClaims"/> so that scope-based authorization
    ///   policies work identically to Entra/JWT Bearer mode.
    /// </param>
    /// <param name="timeProvider">
    ///   Clock source used to set <c>iat</c>, <c>nbf</c>, and <c>exp</c>.
    ///   Pass <see cref="TimeProvider.System"/> in production callers and a
    ///   <see cref="Microsoft.Extensions.Time.Testing.FakeTimeProvider"/> in unit tests.
    ///   Never substitute <c>DateTime.UtcNow</c> here (APL0003).
    /// </param>
    /// <param name="lifetime">Token lifetime. Defaults to one hour.</param>
    /// <returns>A compact-serialized signed JWT string.</returns>
    public static string Mint(
        string key,
        string issuer,
        string audience,
        IEnumerable<string> scopes,
        TimeProvider timeProvider,
        TimeSpan? lifetime = null,
        string? subject = null)
    {
        var now     = timeProvider.GetUtcNow().UtcDateTime;
        var expires = now + (lifetime ?? TimeSpan.FromHours(1));

        var signingKey  = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim> { new("scope", string.Join(" ", scopes)) };
        if (subject is not null)
            claims.Add(new Claim("sub", subject));

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer            = issuer,
            Audience          = audience,
            Subject           = new ClaimsIdentity(claims),
            NotBefore         = now,
            IssuedAt          = now,
            Expires           = expires,
            SigningCredentials = credentials,
        };

        return new JwtSecurityTokenHandler().CreateEncodedJwt(descriptor);
    }
}
