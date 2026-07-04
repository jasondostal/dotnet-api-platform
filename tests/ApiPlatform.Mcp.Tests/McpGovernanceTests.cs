using System.Text.Json;
using ApiPlatform.Mcp.Mcp;
using ApiPlatform.Mcp.Tests.Stubs;
using ApiPlatform.Platform.Auth;
using ApiPlatform.Platform.Errors;
using ApiPlatform.Platform.Pii;

namespace ApiPlatform.Mcp.Tests;

/// <summary>
/// Verifies that PlatformToolset enforces scope governance and PII masking.
/// These are unit tests: all dependencies are stubs; no host or network required.
/// </summary>
public class McpGovernanceTests
{
    private static (PlatformToolset Toolset, CapturingAudit Audit) Build()
    {
        var audit   = new CapturingAudit();
        var toolset = new PlatformToolset(
            new StubAccountSource(),
            new StubCustomerSource(),
            new DefaultPiiRedactor(),
            audit);
        return (toolset, audit);
    }

    private static readonly IReadOnlyDictionary<string, object?> NoArgs =
        new Dictionary<string, object?>();

    // ── 1. Missing scope → Forbidden + audit denial ───────────────────────────

    [Fact]
    public async Task AccountsList_NoScope_ReturnsForbiddenAndAuditsDenial()
    {
        var (toolset, audit) = Build();
        var ctx = new ToolCallContext { GrantedScopes = [], CallerId = "test-agent" };

        var result = await toolset.CallToolAsync("accounts.list", NoArgs, ctx);

        // Access denied with the platform Forbidden problem type
        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Problem);
        Assert.Equal(ProblemTypes.Forbidden.Type,   result.Problem!.Type);
        Assert.Equal(ProblemTypes.Forbidden.Status, result.Problem!.Status);

        // Exactly one audit record written for the denial
        Assert.Single(audit.Events);
        Assert.Equal("tool.denied", audit.Events[0].EventType);
    }

    // ── 2. With required scope → success, PII masked, call audited ────────────

    [Fact]
    public async Task AccountsList_WithScope_SucceedsMasksPiiAndAuditsCall()
    {
        var (toolset, audit) = Build();
        var ctx = new ToolCallContext
        {
            GrantedScopes = [PlatformScopes.AccountRead],
            CallerId      = "test-agent"
        };

        var result = await toolset.CallToolAsync("accounts.list", NoArgs, ctx);

        // Call succeeded
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Content);

        // Serialise to JSON so we can inspect field values
        var json = JsonSerializer.Serialize(result.Content);

        // PII masking applied: the raw account number display must not appear
        Assert.DoesNotContain(StubAccountSource.RawAccountNumberDisplay, json,
            StringComparison.Ordinal);

        // Masked marker must be present (DefaultPiiRedactor.Mask returns "***")
        Assert.Contains("***", json, StringComparison.Ordinal);

        // No raw email address present in any form (accounts carry no email fields)
        Assert.DoesNotContain("@", json, StringComparison.Ordinal);

        // Exactly one audit record written for the successful call
        Assert.Single(audit.Events);
        Assert.Equal("tool.called", audit.Events[0].EventType);
    }
}
