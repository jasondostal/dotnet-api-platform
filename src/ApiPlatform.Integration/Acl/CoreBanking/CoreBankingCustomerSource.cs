using ApiPlatform.Contracts;
using ApiPlatform.Integration.Acl;
using ApiPlatform.Platform.Pii;

namespace ApiPlatform.Integration.Acl.CoreBanking;

/// <summary>
/// ICustomerSource implementation for Core Banking.
/// Backed by <see cref="StubCoreBankingClient"/>; swap for a real client without changing endpoints.
/// <see cref="IPiiRedactor"/> is wired for audit/diagnostic masking — it does not alter
/// the data returned to authorized callers; contact projection remains scope-gated at the endpoint.
/// </summary>
internal sealed class CoreBankingCustomerSource : ICustomerSource
{
    private readonly StubCoreBankingClient _client;
    private readonly IPiiRedactor _piiRedactor;

    public CoreBankingCustomerSource(StubCoreBankingClient client, IPiiRedactor piiRedactor)
    {
        _client      = client;
        _piiRedactor = piiRedactor;
    }

    public Task<CustomerList> ListCustomersAsync(string? cursor, int limit, CancellationToken ct = default)
    {
        var all    = _client.GetCustomers();
        int offset = DecodeOffset(cursor);
        // Return shallow copies so endpoints can safely null-out the contact field without
        // mutating the backing in-memory store.
        var page       = all.Skip(offset).Take(limit).Select(Clone).ToList();
        int next       = offset + page.Count;
        string? nextCursor = next < all.Count ? EncodeOffset(next) : null;

        return Task.FromResult(new CustomerList { Data = page, NextCursor = nextCursor });
    }

    public Task<Customer?> GetCustomerAsync(Guid customerId, CancellationToken ct = default)
    {
        var customer = _client.GetCustomers().FirstOrDefault(c => c.CustomerId == customerId);
        return Task.FromResult(customer is null ? null : Clone(customer));
    }

    // ── Audit/diagnostic helpers ──────────────────────────────────────────────

    /// <summary>
    /// Returns a masked contact summary suitable for audit trails.
    /// Does NOT affect data returned to API callers.
    /// </summary>
    internal string BuildMaskedContactSummary(Customer c)
    {
        var email = c.Contact?.Emails?.FirstOrDefault()?.EmailAddress;
        var phone = c.Contact?.Phones?.FirstOrDefault()?.Number;
        return $"customerId={c.CustomerId} email={_piiRedactor.MaskEmail(email)} phone={_piiRedactor.MaskPhone(phone)}";
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>Shallow-copy a Customer so callers can mutate the Contact field without affecting seed data.</summary>
    private static Customer Clone(Customer c) => new()
    {
        CustomerId = c.CustomerId,
        Name       = c.Name,
        Status     = c.Status,
        Contact    = c.Contact,
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
