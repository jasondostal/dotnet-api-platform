using System.Net;
using System.Net.Http.Headers;
using System.Text;
using ApiPlatform.Platform.AspNetCore.Auth;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using ApiPlatform.Platform.AspNetCore.Runtime;

namespace ApiPlatform.Tests;

/// <summary>
/// Fail-closed matrix for AUTH_MODE=LocalJwt.
///
/// Because <see cref="WebApplicationFactory{TEntryPoint}"/> for minimal-API programs
/// cannot reliably inject configuration before <c>builder.AddPlatform()</c> reads
/// <c>AUTH_MODE</c> (the <c>IWebHostBuilder</c> adapter runs after the builder phase),
/// the 401/403/200 matrix tests override auth services directly via
/// <c>ConfigureServices</c>, using the same constants (<see cref="LocalDevJwt.DefaultDevKey"/>,
/// issuer, audience) that the real LocalJwt branch in <c>AddPlatform</c> uses.
/// This validates the JWT→scope→policy chain end-to-end with the same keys.
///
/// The fail-closed startup-throw test goes one level lower: it calls
/// <c>AddPlatform</c> directly on a <see cref="WebApplicationBuilder"/> with
/// <c>AUTH_MODE=LocalJwt</c> and no <c>AUTH_SIGNING_KEY</c>, with the environment
/// set to "Production", and asserts the <see cref="InvalidOperationException"/>.
/// </summary>
public class LocalJwtAuthTests
{
    // ── JWT-wired factory: overrides auth services to use LocalJwt parameters ──

    /// <summary>
    /// Factory that swaps the auth stack to JwtBearer using the same
    /// <see cref="LocalDevJwt"/> constants the production LocalJwt branch uses.
    /// <c>ConfigureServices</c> runs after the app's services are registered, so
    /// it can add a second scheme and promote it as the default.
    /// </summary>
    private sealed class LocalJwtServicesFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                var keyBytes = Encoding.UTF8.GetBytes(LocalDevJwt.DefaultDevKey);

                // Add the JwtBearer scheme (may coexist with ScopeHeaderAuthHandler)
                services.AddAuthentication()
                        .AddJwtBearer(opts =>
                        {
                            opts.MapInboundClaims = false;
                            opts.TokenValidationParameters = new TokenValidationParameters
                            {
                                ValidateIssuerSigningKey = true,
                                ValidateIssuer           = true,
                                ValidateAudience         = true,
                                ValidateLifetime         = true,
                                ValidIssuer              = LocalDevJwt.DefaultIssuer,
                                ValidAudience            = LocalDevJwt.DefaultAudience,
                                IssuerSigningKey         = new SymmetricSecurityKey(keyBytes),
                            };
                        });

                // Promote JwtBearer as the default authenticate/challenge/forbid scheme
                services.PostConfigure<Microsoft.AspNetCore.Authentication.AuthenticationOptions>(opts =>
                {
                    opts.DefaultScheme          = JwtBearerDefaults.AuthenticationScheme;
                    opts.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                    opts.DefaultChallengeScheme  = JwtBearerDefaults.AuthenticationScheme;
                    opts.DefaultForbidScheme     = JwtBearerDefaults.AuthenticationScheme;
                });

                // Claims transformation: split space-delimited scope claim into
                // individual "scope" claims so scope policies work identically
                services.AddSingleton<Microsoft.AspNetCore.Authentication.IClaimsTransformation,
                                      EntraScopeClaims>();
            });
        }
    }

    // ── Helper ────────────────────────────────────────────────────────────────

    private static HttpClient ClientWithToken(WebApplicationFactory<Program> factory, string token)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    // ── Behaviour matrix (401 / 403 / 200) ───────────────────────────────────

    /// <summary>No Authorization header → JwtBearer challenge → 401.</summary>
    [Fact]
    public async Task NoToken_Returns401()
    {
        using var factory = new LocalJwtServicesFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/v1/accounts");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>Valid signed token, wrong scope → authorization policy denied → 403.</summary>
    [Fact]
    public async Task ValidToken_MissingRequiredScope_Returns403()
    {
        using var factory = new LocalJwtServicesFactory();

        // customer.read is valid but /v1/accounts requires account.read
        var token = LocalDevJwt.Mint(
            LocalDevJwt.DefaultDevKey,
            LocalDevJwt.DefaultIssuer,
            LocalDevJwt.DefaultAudience,
            scopes: ["customer.read"],
            TimeProvider.System);

        var response = await ClientWithToken(factory, token).GetAsync("/v1/accounts");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>Valid signed token with required scope → endpoint authorized → 200.</summary>
    [Fact]
    public async Task ValidToken_WithRequiredScope_Returns200()
    {
        using var factory = new LocalJwtServicesFactory();

        var token = LocalDevJwt.Mint(
            LocalDevJwt.DefaultDevKey,
            LocalDevJwt.DefaultIssuer,
            LocalDevJwt.DefaultAudience,
            scopes: ["account.read"],
            TimeProvider.System);

        var response = await ClientWithToken(factory, token).GetAsync("/v1/accounts");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ── Real-branch end-to-end via in-process TestServer ─────────────────────

    /// <summary>
    /// Drives the SHIPPED <c>AddPlatform</c> LocalJwt branch end-to-end (no re-wired
    /// copy of the auth stack). Composes a fresh <see cref="WebApplicationBuilder"/> in the
    /// Development environment with <c>AUTH_MODE=LocalJwt</c> and an explicit
    /// <c>AUTH_SIGNING_KEY</c> = <see cref="LocalDevJwt.DefaultDevKey"/>, then exercises the
    /// real JwtBearer <c>TokenValidationParameters</c> and the real <c>EntraScopeClaims</c>
    /// registration through an in-process <see cref="TestServer"/>:
    ///   no token → 401, valid token + account.read → 200, valid token + only customer.read → 403.
    /// A regression in the shipped branch's validation params or claims wiring fails here.
    /// </summary>
    [Fact]
    public async Task RealAddPlatformBranch_OverTestServer_EnforcesTokenAndScope()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Development,
        });
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AUTH_MODE"]        = "LocalJwt",
            ["AUTH_SIGNING_KEY"] = LocalDevJwt.DefaultDevKey,
        });

        // The REAL branch under test — configures JwtBearer + EntraScopeClaims + scope policies.
        builder.AddPlatform();

        await using var app = builder.Build();
        app.UsePlatform();
        app.MapGet("/protected", () => Results.Ok(new { ok = true }))
           .RequireAuthorization("account.read");

        await app.StartAsync();
        try
        {
            var client = app.GetTestClient();

            // 1. No Authorization header → JwtBearer challenge → 401
            var noToken = await client.GetAsync("/protected");
            Assert.Equal(HttpStatusCode.Unauthorized, noToken.StatusCode);

            // 2. Valid signed token WITH account.read → 200
            var goodToken = LocalDevJwt.Mint(
                LocalDevJwt.DefaultDevKey,
                LocalDevJwt.DefaultIssuer,
                LocalDevJwt.DefaultAudience,
                scopes: ["account.read"],
                TimeProvider.System);
            var authorized = new HttpRequestMessage(HttpMethod.Get, "/protected");
            authorized.Headers.Authorization = new AuthenticationHeaderValue("Bearer", goodToken);
            var okResponse = await client.SendAsync(authorized);
            Assert.Equal(HttpStatusCode.OK, okResponse.StatusCode);

            // 3. Valid signed token with only customer.read → policy denied → 403
            var wrongScopeToken = LocalDevJwt.Mint(
                LocalDevJwt.DefaultDevKey,
                LocalDevJwt.DefaultIssuer,
                LocalDevJwt.DefaultAudience,
                scopes: ["customer.read"],
                TimeProvider.System);
            var forbiddenReq = new HttpRequestMessage(HttpMethod.Get, "/protected");
            forbiddenReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", wrongScopeToken);
            var forbidden = await client.SendAsync(forbiddenReq);
            Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        }
        finally
        {
            await app.StopAsync();
        }
    }

    // ── Fail-closed startup check ─────────────────────────────────────────────

    /// <summary>
    /// AUTH_MODE=LocalJwt with no AUTH_SIGNING_KEY in a non-Development environment must
    /// throw <see cref="InvalidOperationException"/> at composition time (inside
    /// <c>AddPlatform</c>) — fail closed, never silent allow-all.
    ///
    /// Tested by calling <c>AddPlatform</c> directly on a fresh <see cref="WebApplicationBuilder"/>
    /// configured for the Production environment, avoiding the WebApplicationFactory start-up
    /// interception layer entirely.
    /// </summary>
    [Fact]
    public void NoSigningKey_NonDevelopmentEnvironment_ThrowsInvalidOperationExceptionOnStartup()
    {
        // Set ASPNETCORE_ENVIRONMENT so WebApplication.CreateBuilder picks up "Production"
        var previousEnv = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Production");
        try
        {
            var builder = WebApplication.CreateBuilder();
            // Inject LocalJwt mode with no signing key
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AUTH_MODE"] = "LocalJwt",
                // AUTH_SIGNING_KEY intentionally absent
            });

            var ex = Assert.Throws<InvalidOperationException>(
                () => { builder.AddPlatform(); });

            Assert.Contains("AUTH_SIGNING_KEY", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            // Restore environment so other tests are unaffected
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", previousEnv);
        }
    }

    /// <summary>
    /// Walks the exception chain (including AggregateException.InnerExceptions) to find
    /// an <see cref="InvalidOperationException"/>.
    /// </summary>
    private static bool ContainsInvalidOperationException(Exception? ex) =>
        ex switch
        {
            null                      => false,
            InvalidOperationException => true,
            AggregateException ae     => ae.InnerExceptions.Any(ContainsInvalidOperationException),
            _                         => ContainsInvalidOperationException(ex.InnerException),
        };
}
