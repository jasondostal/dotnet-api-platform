using System.Text;
using ApiPlatform.Platform.AspNetCore.Auth;
using ApiPlatform.Platform.AspNetCore.Errors;
using ApiPlatform.Platform.AspNetCore.Idempotency;
using ApiPlatform.Platform.Runtime;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace ApiPlatform.Platform.AspNetCore.Runtime;

/// <summary>
/// Unified entry points for web-hosting layer governance wiring.
/// </summary>
public static class PlatformAspNetCoreExtensions
{
    /// <summary>
    /// Registers all platform governance services:
    /// <list type="bullet">
    ///   <item>Core platform DI (PII redaction, audit)</item>
    ///   <item>Problem Details (RFC 9457) with Instance=path</item>
    ///   <item>In-memory idempotency store</item>
    ///   <item>
    ///     Authentication — three modes selectable via <c>AUTH_MODE</c>:
    ///     <list type="bullet">
    ///       <item><c>Header</c> (default) — dev X-Scopes header scheme, no token required</item>
    ///       <item><c>Entra</c> — Microsoft Entra ID / Azure AD JWT Bearer; configure <c>AUTH_AUTHORITY</c> and <c>AUTH_AUDIENCE</c></item>
    ///       <item>
    ///         <c>LocalJwt</c> — offline HS256 JWT Bearer; set <c>AUTH_SIGNING_KEY</c> (≥ 32 bytes).
    ///         In Development the non-secret <see cref="LocalDevJwt.DefaultDevKey"/> is used as a fallback
    ///         (a startup warning is logged). In any non-Development environment, a missing key causes
    ///         a hard startup failure (<see cref="InvalidOperationException"/>).
    ///         Mint tokens with <see cref="LocalDevJwt.Mint"/>.
    ///       </item>
    ///     </list>
    ///   </item>
    ///   <item>Authorization — one policy per PlatformScopes value</item>
    /// </list>
    /// </summary>
    public static WebApplicationBuilder AddPlatform(this WebApplicationBuilder builder)
    {
        // ── Core platform services ─────────────────────────────────────────────
        builder.Services.AddPlatformCore(builder.Configuration);

        // ── Audit "who": override the system context with the request principal ──
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddSingleton<ApiPlatform.Platform.Audit.IAuditContext, HttpAuditContext>();

        // ── Problem Details (RFC 9457) ─────────────────────────────────────────
        builder.Services.AddProblemDetails(opts =>
        {
            opts.CustomizeProblemDetails = ctx =>
            {
                ctx.ProblemDetails.Instance = ctx.HttpContext.Request.Path;
            };
        });

        // ── Upstream fault handler (maps UpstreamUnavailableException → 502/503) ─
        builder.Services.AddExceptionHandler<UpstreamExceptionHandler>();

        // ── Idempotency ────────────────────────────────────
        // IDEMPOTENCY_STORE=Memory (default)  — single-process ConcurrentDictionary; zero config.
        // IDEMPOTENCY_STORE=Distributed       — IDistributedCache-backed; entries survive restarts.
        //   The default backing is AddDistributedMemoryCache() (in-process, no external infra);
        //   swap to Redis / SQL in real deployments by registering IDistributedCache before calling
        //   AddPlatform().
        var idempotencyStoreName = builder.Configuration["IDEMPOTENCY_STORE"] ?? "Memory";
        if (string.Equals(idempotencyStoreName, "Distributed", StringComparison.OrdinalIgnoreCase))
        {
            // Register the default distributed memory cache only if the host has not already
            // registered a real IDistributedCache (e.g., Redis).
            builder.Services.AddDistributedMemoryCache();
            builder.Services.AddSingleton<IIdempotencyStore, DistributedCacheIdempotencyStore>();
        }
        else
        {
            builder.Services.AddSingleton<IIdempotencyStore, InMemoryIdempotencyStore>();
        }

        // ── Authentication ─────────────────────────────────────────────────────
        var authMode = builder.Configuration["AUTH_MODE"] ?? "Header";

        if (string.Equals(authMode, "Entra", StringComparison.OrdinalIgnoreCase))
        {
            var authority = builder.Configuration["AUTH_AUTHORITY"];
            var audience  = builder.Configuration["AUTH_AUDIENCE"];

            builder.Services
                .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(opts =>
                {
                    opts.Authority = authority;
                    opts.Audience  = audience;
                });

            builder.Services.AddSingleton<IClaimsTransformation, EntraScopeClaims>();
        }
        else if (string.Equals(authMode, "LocalJwt", StringComparison.OrdinalIgnoreCase))
        {
            // ── LocalJwt: offline HS256 — real tokens, no cloud dependency ─────
            var signingKey = builder.Configuration["AUTH_SIGNING_KEY"];
            var issuer     = builder.Configuration["AUTH_ISSUER"]   ?? LocalDevJwt.DefaultIssuer;
            var audience   = builder.Configuration["AUTH_AUDIENCE"] ?? LocalDevJwt.DefaultAudience;

            if (string.IsNullOrWhiteSpace(signingKey))
            {
                if (!builder.Environment.IsDevelopment())
                {
                    throw new InvalidOperationException(
                        "AUTH_MODE=LocalJwt requires AUTH_SIGNING_KEY to be configured in any " +
                        "non-Development environment. Refusing to start with an unconfigured signing " +
                        "key — this would silently allow-all in production. " +
                        "Set AUTH_SIGNING_KEY to a secret value of at least 32 bytes.");
                }

                // Development fallback: use the non-secret dev key and warn at startup
                signingKey = LocalDevJwt.DefaultDevKey;
                builder.Services.AddSingleton<IStartupFilter>(sp =>
                    new DevKeyWarnStartupFilter(
                        sp.GetRequiredService<ILoggerFactory>()
                          .CreateLogger(nameof(PlatformAspNetCoreExtensions)),
                        "AUTH_MODE=LocalJwt: AUTH_SIGNING_KEY is not set — falling back to the " +
                        "non-secret dev key (LocalDevJwt.DefaultDevKey). " +
                        "Set AUTH_SIGNING_KEY before using in any shared or production environment."));
            }

            var keyBytes = Encoding.UTF8.GetBytes(signingKey);

            builder.Services
                .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(opts =>
                {
                    opts.MapInboundClaims = false;
                    opts.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuerSigningKey = true,
                        ValidateIssuer           = true,
                        ValidateAudience         = true,
                        ValidateLifetime         = true,
                        ValidIssuer              = issuer,
                        ValidAudience            = audience,
                        IssuerSigningKey         = new SymmetricSecurityKey(keyBytes),
                    };
                });

            // Reuse EntraScopeClaims: it splits the space-delimited scp/scope JWT claim
            // into individual PlatformScopes.ScopeClaimType claims so that the same scope
            // policies (RequireClaim("scope", value)) work identically across all JWT modes.
            builder.Services.AddSingleton<IClaimsTransformation, EntraScopeClaims>();
        }
        else
        {
            // Default: dev/test header scheme — swap via AUTH_MODE=Entra for production
            builder.Services
                .AddAuthentication(ScopeHeaderAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, ScopeHeaderAuthHandler>(
                    ScopeHeaderAuthHandler.SchemeName, _ => { });
        }

        // ── Authorization ──────────────────────────────────────────────────────
        builder.Services.AddAuthorization(ScopePolicies.AddPlatformScopePolicies);

        return builder;
    }

    /// <summary>
    /// Applies platform governance middleware in the correct order:
    /// exception handling, status-code pages, authentication, authorization,
    /// and idempotency (after auth, before endpoint execution).
    /// </summary>
    public static WebApplication UsePlatform(this WebApplication app)
    {
        app.UseExceptionHandler();
        app.UseStatusCodePages();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseMiddleware<IdempotencyMiddleware>();
        return app;
    }
}

/// <summary>
/// Emits a single startup warning log and passes the configure pipeline through unchanged.
/// Registered as a transient <see cref="IStartupFilter"/> when LocalJwt mode falls back
/// to the non-secret dev key.
/// </summary>
file sealed class DevKeyWarnStartupFilter(ILogger logger, string message) : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        logger.LogWarning("{Message}", message);
        return next;
    }
}
