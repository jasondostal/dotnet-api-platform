using System.Net;
using System.Net.Http.Json;
using ApiPlatform.Platform.Pii;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace ApiPlatform.Tests;

/// <summary>
/// Proves the direct <c>Audit.Core.AuditScope.Log("Account:Touched", …)</c> write in the touch
/// endpoint no longer stores a raw account id: the id is masked via <see cref="IPiiRedactor"/>
/// before it reaches the audit store, while the operation label stays legible.
///
/// The endpoint writes through the process-global Audit.NET file provider, so this class uses a
/// dedicated factory pointed at a unique AUDIT_LOG_DIR and reads the emitted audit file directly,
/// mirroring the file-based audit-assertion harness used by the source-governance tests.
/// </summary>
public class TouchAuditMaskingTests
{
    // Seeded DEPOSIT account id (from StubCoreBankingClient) — touch requires the account to exist.
    private const string DepositId = "f47ac10b-58cc-4372-a567-0e02b2c3d479";

    [Fact]
    public async Task TouchAccount_AuditEntry_MasksRawAccountId()
    {
        var auditDir = Path.Combine(Path.GetTempPath(), $"touch-audit-{Guid.NewGuid():N}");
        Directory.CreateDirectory(auditDir);

        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(b => b.UseSetting("AUDIT_LOG_DIR", auditDir));

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Scopes", "event.publish");

        var response = await client.PostAsync($"/v1/accounts/{DepositId}/touch", content: null);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        // The masking transformation the handler applies, resolved from the app's own DI container —
        // the exact IPiiRedactor instance the endpoint feeds the raw id through.
        var redactor = factory.Services.GetRequiredService<IPiiRedactor>();
        var maskedId = redactor.Mask(DepositId);
        Assert.Equal("***", maskedId);
        Assert.DoesNotContain(DepositId, maskedId, StringComparison.OrdinalIgnoreCase);

        // Read the audit file this factory wrote to (unique dir → only this run's entries).
        var files = Directory.GetFiles(auditDir, "audit-*.json");
        Assert.NotEmpty(files);
        var content = string.Concat(files.Select(File.ReadAllText));

        // Locate the Account:Touched entry — the operation label stays legible.
        Assert.Contains("Account:Touched", content, StringComparison.Ordinal);

        // The raw account id must NOT appear anywhere in the audit content, and the masked token must.
        Assert.DoesNotContain(DepositId, content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("***", content, StringComparison.Ordinal);
    }
}
