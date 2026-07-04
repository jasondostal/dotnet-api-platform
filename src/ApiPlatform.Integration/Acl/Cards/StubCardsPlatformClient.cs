namespace ApiPlatform.Integration.Acl.Cards;

/// <summary>
/// In-memory stub for the Cards Platform.
/// Returns seed card and transaction data in vendor-native shape.
/// Swap for a real HTTP client without changing any source or endpoint code.
/// </summary>
public sealed class StubCardsPlatformClient
{
    // ── Vendor-native DTOs (internal — never exposed beyond this folder) ──────

    internal sealed record CardRaw(
        string card_ref,        // vendor-native id — e.g. "CARD-4521-XYZ"
        string masked_pan,      // maps to AccountNumberDisplay
        string card_status,     // "ACTIVE" | "CLOSED" | "FROZEN"
        string product_label,   // maps to ProductName
        decimal credit_line,    // maps to CreditLimit
        decimal balance,        // maps to CurrentBalance
        decimal open_to_buy     // maps to AvailableCredit
        // NOTE: no apr, no minimum_payment_due, no payment_due_date —
        // this card processor does not surface those fields in its API.
    );

    internal sealed record CardTxnRaw(
        Guid txn_id,
        string card_ref,
        string txn_type,        // "DEBIT" | "CREDIT"
        decimal amount,
        string description,
        string status,
        string? posted_date,
        string? txn_date,
        string? merchant_name,
        string? mcc
    );

    // ── Seed data ─────────────────────────────────────────────────────────────

    private static readonly IReadOnlyList<CardRaw> SeedCards =
    [
        new CardRaw(
            card_ref      : "CARD-4521-XYZ",
            masked_pan    : "****4521",
            card_status   : "ACTIVE",
            product_label : "Platinum Rewards",
            credit_line   : 10_000.00m,
            balance       :  2_547.89m,
            open_to_buy   :  7_452.11m
        ),
    ];

    private static readonly IReadOnlyList<CardTxnRaw> SeedTransactions =
    [
        new CardTxnRaw(
            txn_id        : Guid.Parse("550e8400-e29b-41d4-a716-446655440000"),
            card_ref      : "CARD-4521-XYZ",
            txn_type      : "DEBIT",
            amount        : 42.17m,
            description   : "COFFEE ROASTERS #221",
            status        : "POSTED",
            posted_date   : "2026-06-22",
            txn_date      : "2026-06-21",
            merchant_name : "Coffee Roasters",
            mcc           : "5812"
        ),
        new CardTxnRaw(
            txn_id        : Guid.Parse("9b2e6679-7425-40de-944b-e07fc1f90123"),
            card_ref      : "CARD-4521-XYZ",
            txn_type      : "CREDIT",
            amount        : 500.00m,
            description   : "PAYMENT - THANK YOU",
            status        : "POSTED",
            posted_date   : "2026-06-20",
            txn_date      : null,
            merchant_name : null,
            mcc           : null
        ),
    ];

    internal IReadOnlyList<CardRaw> GetCards() => SeedCards;

    internal IReadOnlyList<CardTxnRaw> GetTransactions(string cardRef) =>
        SeedTransactions.Where(t => t.card_ref == cardRef).ToList();
}
