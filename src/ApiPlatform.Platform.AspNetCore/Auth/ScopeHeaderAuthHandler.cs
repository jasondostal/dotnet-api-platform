using System.Security.Claims;
using System.Text.Encodings.Web;
using ApiPlatform.Platform.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ApiPlatform.Platform.AspNetCore.Auth;

/// <summary>
/// Dev/test authentication handler that reads scopes from the X-Scopes request header
/// (space-delimited). Produces a ClaimsPrincipal with one "scope" claim per token plus
/// a Name claim, making it trivial to swap for JWT Bearer in production via AUTH_MODE=Entra.
/// </summary>
public sealed class ScopeHeaderAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "ScopeHeader";

    public ScopeHeaderAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder) { }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var header = Request.Headers["X-Scopes"].ToString();
        var scopes = header.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var claims = scopes
            .Select(s => new Claim(PlatformScopes.ScopeClaimType, s))
            .Append(new Claim(ClaimTypes.Name, "dev-caller"))
            .ToList();

        var identity  = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket    = new AuthenticationTicket(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
