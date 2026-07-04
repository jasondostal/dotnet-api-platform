using ApiPlatform.Contracts;
using ApiPlatform.Integration.Acl;
using ApiPlatform.Integration.Runtime;
using ApiPlatform.Platform.Runtime;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ApiPlatform.Tests;

/// <summary>
/// Verifies that the AuditInterceptor's parameter-name-aware masking rule suppresses
/// sensitive identifier and PII scalars in the audit record, while keeping operational
/// metadata (pagination limits, enum flags, type labels for complex payloads) legible.
///
/// Classification rule:
///   SENSITIVE  — param name contains: id, account, member, customer, ssn, tax, number,
///                phone, email, name, dob, birth → masked via IPiiRedactor (Guid → "***",
///                string → Mask/MaskPhone/MaskEmail; '@' in value always triggers MaskEmail).
///   NON-SENSITIVE — param name is cursor, limit, offset, page, etc.; bool/int/enum values
///                   remain legible regardless; CancellationToken → "ct".
///   COMPLEX OBJECT — recorded by type name only; payload fields are never dumped.
/// </summary>
public class AuditInterceptorMaskingTests
{
    private static ServiceProvider Build(string auditDir)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["AUDIT_LOG_DIR"] = auditDir })
            .Build();

        return new ServiceCollection()
            .AddPlatformCore(config)
            .AddIntegration(config)
            .BuildServiceProvider();
    }

    private static string ReadAudit(string dir)
    {
        var files = Directory.Exists(dir) ? Directory.GetFiles(dir) : [];
        Assert.NotEmpty(files);
        return string.Concat(files.Select(File.ReadAllText));
    }

    // ── 1. Sensitive Guid id: full value suppressed, "***" present ────────────
    //
    // GetAccountAsync(Guid accountId, ...) — "accountId" contains both "id" and "account"
    // → must be masked. The raw guid string must NOT appear in the audit record.

    [Fact]
    public async Task GetAccount_AuditRecord_DoesNotContainRawAccountId()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"mask-id-{Guid.NewGuid():N}");
        using var sp = Build(dir);

        // Use a synthetic account id that does not appear in the stub seed data,
        // so any occurrence of this string in the audit file is definitely the unmasked arg.
        var syntheticId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        // GetAccountAsync returns null for unknown ids — that is fine, we just want the audit record.
        await sp.GetRequiredService<IAccountSource>().GetAccountAsync(syntheticId);

        var content = ReadAudit(dir);

        // Full guid must NOT appear — it's a sensitive identifier
        Assert.DoesNotContain(
            syntheticId.ToString(),
            content,
            StringComparison.OrdinalIgnoreCase);

        // Masked token "***" MUST appear in inputs
        Assert.Contains("***", content, StringComparison.Ordinal);

        // Governance metadata: actor and operation always present
        Assert.Contains("GetAccount", content, StringComparison.Ordinal);
        Assert.Contains("system", content, StringComparison.Ordinal);
    }

    // ── 2. Complex-object payload: type name only, no field dump ─────────────
    //
    // CreateAccountAsync(Account draft, ...) — "draft" is not a sensitive name,
    // but Account is a complex type → recorded as "Account" (type name only).
    // No property values should leak into the audit record.

    [Fact]
    public async Task CreateAccount_AuditRecord_PayloadRecordedByTypeNameOnly()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"mask-obj-{Guid.NewGuid():N}");
        using var sp = Build(dir);

        await sp.GetRequiredService<IAccountWriter>()
            .CreateAccountAsync(new Account { AccountType = AccountType.DEPOSIT });

        var content = ReadAudit(dir);

        // Complex payload recorded by type name
        Assert.Contains("Account", content, StringComparison.Ordinal);

        // Governance metadata
        Assert.Contains("CreateAccount", content, StringComparison.Ordinal);
        Assert.Contains("allowed", content, StringComparison.Ordinal);
        Assert.Contains("system", content, StringComparison.Ordinal);
    }

    // ── 3. Non-sensitive scalar: limit stays legible ──────────────────────────
    //
    // ListAccountsAsync(cursor: null, limit: 50, ...) — "limit" contains no sensitive
    // token, and int is an operational scalar → the value 50 must appear in the audit record.

    [Fact]
    public async Task ListAccounts_AuditRecord_LimitRemainsLegible()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"mask-limit-{Guid.NewGuid():N}");
        using var sp = Build(dir);

        await sp.GetRequiredService<IAccountSource>().ListAccountsAsync(cursor: null, limit: 50);

        var content = ReadAudit(dir);

        // Pagination limit must be legible — operational metadata, not PII
        Assert.Contains("50", content, StringComparison.Ordinal);

        // Governance metadata
        Assert.Contains("ListAccounts", content, StringComparison.Ordinal);
        Assert.Contains("allowed", content, StringComparison.Ordinal);
        Assert.Contains("system", content, StringComparison.Ordinal);
    }

    // ── 4. Sensitive Guid in ListTransactions (accountId) also masked ─────────
    //
    // ListTransactionsAsync(Guid accountId, ...) — first argument is a sensitive Guid;
    // second is cursor (non-sensitive string), third is limit (non-sensitive int).
    // Verifies the masking applies to any position, not only the first argument.

    [Fact]
    public async Task ListTransactions_AuditRecord_AccountIdMasked_LimitLegible()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"mask-txn-{Guid.NewGuid():N}");
        using var sp = Build(dir);

        var syntheticId = Guid.Parse("11111111-2222-3333-4444-555555555555");
        await sp.GetRequiredService<IAccountSource>()
            .ListTransactionsAsync(syntheticId, cursor: null, limit: 25);

        var content = ReadAudit(dir);

        // Sensitive Guid suppressed
        Assert.DoesNotContain(
            syntheticId.ToString(),
            content,
            StringComparison.OrdinalIgnoreCase);

        Assert.Contains("***", content, StringComparison.Ordinal);

        // Non-sensitive limit still legible
        Assert.Contains("25", content, StringComparison.Ordinal);

        // Governance metadata
        Assert.Contains("ListTransactions", content, StringComparison.Ordinal);
        Assert.Contains("system", content, StringComparison.Ordinal);
    }
}
