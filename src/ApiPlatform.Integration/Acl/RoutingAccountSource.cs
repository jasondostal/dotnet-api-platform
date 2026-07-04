using ApiPlatform.Contracts;

namespace ApiPlatform.Integration.Acl;

/// <summary>
/// Implements IAccountSource by aggregating multiple IAccountVendor instances.
/// Vendors are queried in registration order (Core Banking first, Cards Platform second).
/// Consumers of IAccountSource cannot observe which vendor served any given account —
/// the canonical contract is preserved end-to-end.
/// </summary>
/// <remarks>
/// If ANY vendor call fails (throws any exception), the aggregate operation fails with an
/// <see cref="Platform.Errors.UpstreamUnavailableException"/> carrying the classified
/// <see cref="Platform.Results.UpstreamOutcome"/>. A partial/silent contribution is never
/// returned when a vendor is unhealthy.
/// </remarks>
internal sealed class RoutingAccountSource : IAccountSource
{
    private readonly IReadOnlyList<IAccountVendor> _vendors;

    public RoutingAccountSource(IEnumerable<IAccountVendor> vendors)
    {
        _vendors = vendors.ToList();
    }

    public async Task<AccountList> ListAccountsAsync(string? cursor, int limit, CancellationToken ct = default)
    {
        var allAccounts = new List<Account>();
        foreach (var vendor in _vendors)
        {
            var result = await VendorExecution.ExecuteAsync(() => vendor.GetAccountsAsync(ct), ct).ConfigureAwait(false);
            VendorExecution.ThrowIfUpstreamError(result, vendor.SourceSystem);
            if (result.IsSuccess)
                allAccounts.AddRange(result.Value);
        }

        int offset = DecodeOffset(cursor);
        var page   = allAccounts.Skip(offset).Take(limit).Select(Clone).ToList();
        int next   = offset + page.Count;
        string? nextCursor = next < allAccounts.Count ? EncodeOffset(next) : null;

        return new AccountList { Data = page, NextCursor = nextCursor };
    }

    public async Task<Account?> GetAccountAsync(Guid accountId, CancellationToken ct = default)
    {
        foreach (var vendor in _vendors)
        {
            var result = await VendorExecution.ExecuteAsync(() => vendor.GetAccountsAsync(ct), ct).ConfigureAwait(false);
            VendorExecution.ThrowIfUpstreamError(result, vendor.SourceSystem);
            if (result.IsSuccess)
            {
                var match = result.Value.FirstOrDefault(a => a.AccountId == accountId);
                if (match is not null)
                    return Clone(match);
            }
        }
        return null;
    }

    public async Task<TransactionList> ListTransactionsAsync(Guid accountId, string? cursor, int limit, CancellationToken ct = default)
    {
        List<Transaction> all = [];
        foreach (var vendor in _vendors)
        {
            var accountsResult = await VendorExecution.ExecuteAsync(() => vendor.GetAccountsAsync(ct), ct).ConfigureAwait(false);
            VendorExecution.ThrowIfUpstreamError(accountsResult, vendor.SourceSystem);

            if (accountsResult.IsSuccess && accountsResult.Value.Any(a => a.AccountId == accountId))
            {
                var txnsResult = await VendorExecution.ExecuteAsync(() => vendor.GetTransactionsAsync(accountId, ct), ct).ConfigureAwait(false);
                VendorExecution.ThrowIfUpstreamError(txnsResult, vendor.SourceSystem);
                if (txnsResult.IsSuccess)
                    all.AddRange(txnsResult.Value);
                break; // account belongs to exactly one vendor
            }
        }

        int offset = DecodeOffset(cursor);
        var page   = all.Skip(offset).Take(limit).ToList();
        int next   = offset + page.Count;
        string? nextCursor = next < all.Count ? EncodeOffset(next) : null;

        return new TransactionList { Data = page, NextCursor = nextCursor };
    }

    /// <summary>Shallow-copy an Account so callers can mutate detail fields without affecting vendor seed data.</summary>
    private static Account Clone(Account a) => new()
    {
        AccountId            = a.AccountId,
        AccountType          = a.AccountType,
        AccountNumberDisplay = a.AccountNumberDisplay,
        Nickname             = a.Nickname,
        Status               = a.Status,
        Currency             = a.Currency,
        ProductName          = a.ProductName,
        DepositAccount       = a.DepositAccount,
        CreditAccount        = a.CreditAccount,
        LoanAccount          = a.LoanAccount,
    };

    private static int DecodeOffset(string? cursor)
    {
        if (string.IsNullOrEmpty(cursor)) return 0;
        try
        {
            var json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            return doc.RootElement.GetProperty("offset").GetInt32();
        }
        catch { return 0; }
    }

    private static string EncodeOffset(int offset)
    {
        var json = $"{{\"offset\":{offset}}}";
        return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(json));
    }
}
