using ApiPlatform.Contracts;
using ApiPlatform.Integration.Acl;

namespace ApiPlatform.Integration.Acl.Cards;

/// <summary>
/// IAccountVendor implementation for the Cards Platform.
/// Owns CREDIT accounts. Deliberately translates vendor-native snake_case DTOs to the
/// canonical Account model to demonstrate the ACL's value — consumers receive the same
/// shape regardless of which vendor backs the data.
/// Backed by <see cref="StubCardsPlatformClient"/>; swap for a real HTTP client without
/// changing any endpoint or routing code.
/// </summary>
internal sealed class CardsPlatformAccountSource : IAccountVendor
{
    private readonly StubCardsPlatformClient _client;

    // Vendor-native id → opaque canonical UUID mapping.
    // The Cards Platform identifies cards by a vendor string (card_ref).
    // We map to canonical UUIDs here so the vendor id never escapes this class.
    private static readonly Dictionary<string, Guid> CardRefToCanonicalId = new()
    {
        ["CARD-4521-XYZ"] = Guid.Parse("7c9e6679-7425-40de-944b-e07fc1f90ae7"),
    };

    /// <summary>Parameterless constructor uses the stub by default (supports direct unit-test instantiation).</summary>
    public CardsPlatformAccountSource() : this(new StubCardsPlatformClient()) { }

    public CardsPlatformAccountSource(StubCardsPlatformClient client)
    {
        _client = client;
    }

    public string SourceSystem => "Cards Platform";

    public Task<IReadOnlyList<Account>> GetAccountsAsync(CancellationToken ct = default)
    {
        IReadOnlyList<Account> result = _client.GetCards().Select(MapCard).ToList();
        return Task.FromResult(result);
    }

    public Task<IReadOnlyList<Transaction>> GetTransactionsAsync(Guid accountId, CancellationToken ct = default)
    {
        // Reverse-map canonical UUID → vendor card_ref to filter raw transactions
        var matchingRefs = CardRefToCanonicalId
            .Where(kv => kv.Value == accountId)
            .Select(kv => kv.Key)
            .ToHashSet();

        IReadOnlyList<Transaction> result = matchingRefs
            .SelectMany(cardRef => _client.GetTransactions(cardRef))
            .Select(t => MapTransaction(t, accountId))
            .ToList();

        return Task.FromResult(result);
    }

    // ── Raw → canonical mapping ───────────────────────────────────────────────

    private static Account MapCard(StubCardsPlatformClient.CardRaw raw)
    {
        if (!CardRefToCanonicalId.TryGetValue(raw.card_ref, out var canonicalId))
            canonicalId = Guid.NewGuid(); // fallback for any future cards not yet mapped

        return new Account
        {
            AccountId            = canonicalId,
            AccountType          = AccountType.CREDIT,
            AccountNumberDisplay = raw.masked_pan,
            Nickname             = null,
            Status               = MapStatus(raw.card_status),
            Currency             = "USD",
            ProductName          = raw.product_label,
            CreditAccount = new CreditDetail
            {
                CreditLimit     = raw.credit_line,
                CurrentBalance  = raw.balance,
                AvailableCredit = raw.open_to_buy,
                // Cards Platform does not supply APR or payment fields.
                // Consumers must treat them as optional per the canonical contract.
                PurchaseApr       = null,
                MinimumPaymentDue = null,
                PaymentDueDate    = null,
            },
        };
    }

    private static AccountStatus MapStatus(string cardStatus) => cardStatus switch
    {
        "ACTIVE" => AccountStatus.OPEN,
        "CLOSED" => AccountStatus.CLOSED,
        "FROZEN" => AccountStatus.FROZEN,
        _        => AccountStatus.OPEN,
    };

    private static Transaction MapTransaction(StubCardsPlatformClient.CardTxnRaw raw, Guid canonicalAccountId) => new()
    {
        TransactionId        = raw.txn_id,
        AccountId            = canonicalAccountId,
        TransactionType      = raw.txn_type == "DEBIT" ? TransactionType.DEBIT : TransactionType.CREDIT,
        Amount               = raw.amount,
        Description          = raw.description,
        Status               = raw.status == "POSTED" ? TransactionStatus.POSTED : TransactionStatus.PENDING,
        PostedDate           = raw.posted_date is not null ? DateOnly.Parse(raw.posted_date) : null,
        TransactionDate      = raw.txn_date is not null ? DateOnly.Parse(raw.txn_date) : null,
        MerchantName         = raw.merchant_name,
        MerchantCategoryCode = raw.mcc,
    };
}
