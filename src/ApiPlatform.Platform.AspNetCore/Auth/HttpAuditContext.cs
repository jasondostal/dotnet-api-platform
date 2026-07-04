using System.Security.Claims;
using ApiPlatform.Platform.Audit;
using ApiPlatform.Platform.Auth;
using Microsoft.AspNetCore.Http;

namespace ApiPlatform.Platform.AspNetCore.Auth;

/// <summary>
/// <see cref="IAuditContext"/> backed by the current request's authenticated principal. It reads
/// the ambient <see cref="HttpContext"/> via <see cref="IHttpContextAccessor"/> on each access, so
/// it is safe to inject into the singleton source-seam decorators (no captive request scope) while
/// still naming the real caller + scopes in every audit record.
/// </summary>
public sealed class HttpAuditContext : IAuditContext
{
    private readonly IHttpContextAccessor _accessor;

    public HttpAuditContext(IHttpContextAccessor accessor) => _accessor = accessor;

    private ClaimsPrincipal? User => _accessor.HttpContext?.User;

    public string Actor =>
        User?.FindFirst("sub")?.Value
        ?? User?.Identity?.Name
        ?? "anonymous";

    public IReadOnlyCollection<string> Scopes =>
        User?.FindAll(PlatformScopes.ScopeClaimType).Select(c => c.Value).ToArray()
        ?? [];
}
