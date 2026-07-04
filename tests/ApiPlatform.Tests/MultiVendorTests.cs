using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using ApiPlatform.Contracts;
using ApiPlatform.Integration.Acl.Cards;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ApiPlatform.Tests;

/// <summary>
/// Verifies multi-vendor ACL behaviour: one canonical Account contract served by
/// two heterogeneous source systems (Core Banking + Cards Platform). Consumers
/// cannot tell which vendor backed which account.
/// </summary>
public class MultiVendorTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    // Canonical IDs — same values the spec and existing tests use
    private const string DepositId = "f47ac10b-58cc-4372-a567-0e02b2c3d479";
    private const string LoanId    = "a1b2c3d4-e5f6-7890-abcd-ef1234567890";
    private const string CreditId  = "7c9e6679-7425-40de-944b-e07fc1f90ae7";

    public MultiVendorTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private HttpClient CreateClient(params string[] scopes)
    {
        var client = _factory.CreateClient();
        if (scopes.Length > 0)
            client.DefaultRequestHeaders.Add("X-Scopes", string.Join(" ", scopes));
        return client;
    }

    // ── 1. All 3 accounts present regardless of vendor origin ─────────────────

    [Fact]
    public async Task ListAccounts_ReturnsAllThreeAccounts_AcrossBothVendors()
    {
        var client = CreateClient("account.read");
        var response = await client.GetAsync("/v1/accounts");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonObject>();
        var data = body!["data"]!.AsArray();

        Assert.Equal(3, data.Count);

        var ids = data.Select(a => a!["accountId"]!.GetValue<string>()).ToHashSet();
        Assert.Contains(DepositId, ids);
        Assert.Contains(LoanId,    ids);
        Assert.Contains(CreditId,  ids);
    }

    // ── 2. Credit account has gap fields absent, deposit detail unaffected ─────

    [Fact]
    public async Task ListAccounts_CreditAccount_GapFieldsAbsentFromJson()
    {
        var client = CreateClient("account.read", "account.detailed.read");
        var response = await client.GetAsync("/v1/accounts");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonObject>();
        var data = body!["data"]!.AsArray();

        // Find the credit account by type
        var credit = data.FirstOrDefault(a => a!["accountId"]!.GetValue<string>() == CreditId)?.AsObject();
        Assert.NotNull(credit);

        var creditDetail = credit!["creditAccount"]?.AsObject();
        Assert.NotNull(creditDetail);

        // Core balance fields ARE present
        Assert.True(creditDetail!.ContainsKey("creditLimit"),    "creditLimit should be present");
        Assert.True(creditDetail!.ContainsKey("currentBalance"), "currentBalance should be present");
        Assert.True(creditDetail!.ContainsKey("availableCredit"),"availableCredit should be present");

        // Gap fields MUST be absent (Cards Platform doesn't supply them)
        Assert.False(creditDetail!.ContainsKey("purchaseApr"),   "purchaseApr should be absent — Cards Platform coverage gap");
        Assert.False(creditDetail!.ContainsKey("paymentDueDate"),"paymentDueDate should be absent — Cards Platform coverage gap");
    }

    [Fact]
    public async Task ListAccounts_DepositAccount_DetailUnaffectedByCardGap()
    {
        var client = CreateClient("account.read", "account.detailed.read");
        var response = await client.GetAsync("/v1/accounts");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonObject>();
        var data = body!["data"]!.AsArray();

        var deposit = data.FirstOrDefault(a => a!["accountId"]!.GetValue<string>() == DepositId)?.AsObject();
        Assert.NotNull(deposit);

        var depositDetail = deposit!["depositAccount"]?.AsObject();
        Assert.NotNull(depositDetail);
        Assert.True(depositDetail!.ContainsKey("availableBalance"), "availableBalance should be present for deposit");
        Assert.True(depositDetail!.ContainsKey("currentBalance"),   "currentBalance should be present for deposit");
    }

    // ── 3. GET single account routes to the correct vendor ────────────────────

    [Fact]
    public async Task GetAccount_CreditId_RoutesToCardsPlatform_ReturnsCanonicalShape()
    {
        var client = CreateClient("account.read", "account.detailed.read");
        var response = await client.GetAsync($"/v1/accounts/{CreditId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonObject>();
        Assert.NotNull(body);

        // Canonical id — not the vendor's card_ref string
        Assert.Equal(CreditId, body!["accountId"]!.GetValue<string>());
        Assert.Equal("CREDIT",  body!["accountType"]!.GetValue<string>());
        Assert.Equal("OPEN",    body!["status"]!.GetValue<string>());
        Assert.Equal("****4521",body!["accountNumberDisplay"]!.GetValue<string>());

        // Detail present
        var creditDetail = body["creditAccount"]?.AsObject();
        Assert.NotNull(creditDetail);

        // Gap fields absent
        Assert.False(creditDetail!.ContainsKey("purchaseApr"));
        Assert.False(creditDetail!.ContainsKey("paymentDueDate"));
    }

    [Fact]
    public async Task GetAccount_DepositId_RoutesToCoreBanking_ReturnsCanonicalShape()
    {
        var client = CreateClient("account.read", "account.detailed.read");
        var response = await client.GetAsync($"/v1/accounts/{DepositId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonObject>();
        Assert.NotNull(body);

        Assert.Equal(DepositId, body!["accountId"]!.GetValue<string>());
        Assert.Equal("DEPOSIT", body!["accountType"]!.GetValue<string>());

        var depositDetail = body["depositAccount"]?.AsObject();
        Assert.NotNull(depositDetail);
        Assert.True(depositDetail!.ContainsKey("availableBalance"));
    }

    // ── 4. Transactions for credit account now served by Cards Platform ────────

    [Fact]
    public async Task ListTransactions_CreditAccount_ReturnsBothCardsTxns()
    {
        var client = CreateClient("transaction.read");
        var response = await client.GetAsync($"/v1/accounts/{CreditId}/transactions");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonObject>();
        var data = body!["data"]!.AsArray();

        Assert.Equal(2, data.Count);

        // Both transactions belong to the canonical credit account id
        foreach (var txn in data)
            Assert.Equal(CreditId, txn!["accountId"]!.GetValue<string>());

        // Specific transactions: coffee debit + payment credit
        var txnIds = data.Select(t => t!["transactionId"]!.GetValue<string>()).ToHashSet();
        Assert.Contains("550e8400-e29b-41d4-a716-446655440000", txnIds); // coffee
        Assert.Contains("9b2e6679-7425-40de-944b-e07fc1f90123", txnIds); // payment
    }

    // ── 5. Unit test: CardsPlatformAccountSource raw→canonical mapping ─────────

    [Fact]
    public async Task CardsPlatformAccountSource_MapToCanonical_CorrectFields()
    {
        var source = new CardsPlatformAccountSource();

        var accounts = await source.GetAccountsAsync();

        Assert.Single(accounts);
        var account = accounts[0];

        // Vendor card_ref "CARD-4521-XYZ" must map to the canonical UUID, not be exposed
        Assert.Equal(Guid.Parse(CreditId), account.AccountId);
        Assert.Equal(AccountStatus.OPEN,   account.Status);
        Assert.Equal(AccountType.CREDIT,   account.AccountType);
        Assert.Equal("****4521",           account.AccountNumberDisplay);
        Assert.Equal("Platinum Rewards",   account.ProductName);

        var detail = account.CreditAccount;
        Assert.NotNull(detail);
        Assert.Equal(10_000.00m, detail!.CreditLimit);
        Assert.Equal( 2_547.89m, detail!.CurrentBalance);
        Assert.Equal( 7_452.11m, detail!.AvailableCredit);

        // Coverage gap fields must be null
        Assert.Null(detail!.PurchaseApr);
        Assert.Null(detail!.MinimumPaymentDue);
        Assert.Null(detail!.PaymentDueDate);
    }

    [Fact]
    public async Task CardsPlatformAccountSource_Transactions_MappedToCanonicalAccountId()
    {
        var source = new CardsPlatformAccountSource();
        var creditId = Guid.Parse(CreditId);

        var txns = await source.GetTransactionsAsync(creditId);

        Assert.Equal(2, txns.Count);

        // All transactions carry the canonical UUID — not the vendor card_ref
        foreach (var t in txns)
            Assert.Equal(creditId, t.AccountId);

        var coffeeTxn = txns.FirstOrDefault(t => t.TransactionId == Guid.Parse("550e8400-e29b-41d4-a716-446655440000"));
        Assert.NotNull(coffeeTxn);
        Assert.Equal(TransactionType.DEBIT, coffeeTxn!.TransactionType);
        Assert.Equal(42.17m, coffeeTxn.Amount);
        Assert.Equal("Coffee Roasters", coffeeTxn.MerchantName);

        var paymentTxn = txns.FirstOrDefault(t => t.TransactionId == Guid.Parse("9b2e6679-7425-40de-944b-e07fc1f90123"));
        Assert.NotNull(paymentTxn);
        Assert.Equal(TransactionType.CREDIT, paymentTxn!.TransactionType);
        Assert.Equal(500.00m, paymentTxn.Amount);
    }
}
