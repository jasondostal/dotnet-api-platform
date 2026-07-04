using ApiPlatform.Contracts;

namespace ApiPlatform.Integration.Acl.CoreBanking;

/// <summary>
/// In-memory stub for the Core Banking system.
/// Returns seed accounts, transactions, and customers identical to the spec examples.
/// Swap for a real HTTP/gRPC client without changing any source or endpoint code.
/// </summary>
public sealed class StubCoreBankingClient
{
    private static readonly Guid DepositAccountId = Guid.Parse("f47ac10b-58cc-4372-a567-0e02b2c3d479");
    private static readonly Guid LoanAccountId    = Guid.Parse("a1b2c3d4-e5f6-7890-abcd-ef1234567890");

    private static readonly IReadOnlyList<Account> SeedAccounts =
    [
        new Account
        {
            AccountId            = DepositAccountId,
            AccountType          = AccountType.DEPOSIT,
            AccountNumberDisplay = "****1234",
            Nickname             = "Everyday Checking",
            Status               = AccountStatus.OPEN,
            Currency             = "USD",
            ProductName          = "Free Checking",
            DepositAccount = new DepositDetail
            {
                CurrentBalance   = 3_412.56m,
                AvailableBalance = 3_312.56m,
            },
        },
        new Account
        {
            AccountId            = LoanAccountId,
            AccountType          = AccountType.LOAN,
            AccountNumberDisplay = "****7890",
            Nickname             = "Auto Loan",
            Status               = AccountStatus.OPEN,
            Currency             = "USD",
            ProductName          = "Fixed-Rate Auto",
            LoanAccount = new LoanDetail
            {
                PrincipalBalance = 14_250.00m,
                InterestRate     =      5.49m,
                PaymentAmount    =    285.00m,
                NextPaymentDate  = new DateOnly(2026, 7, 1),
            },
        },
    ];

    private static readonly IReadOnlyList<Transaction> SeedTransactions =
    [
        new Transaction
        {
            TransactionId   = Guid.Parse("c3d4e5f6-a7b8-9012-cdef-345678901234"),
            AccountId       = DepositAccountId,
            TransactionType = TransactionType.DEBIT,
            Amount          = 125.00m,
            Description     = "NORTHWIND CU TRANSFER",
            Status          = TransactionStatus.POSTED,
            PostedDate      = new DateOnly(2026, 6, 23),
            TransactionDate = new DateOnly(2026, 6, 23),
        },
    ];

    private static readonly Guid AveryId  = Guid.Parse("3f1a7c20-9b54-4e11-a8d3-1c2b3a4d5e6f");
    private static readonly Guid JordanId = Guid.Parse("8c2b1d40-7e65-4a22-b9e4-2d3c4b5a6f70");
    private static readonly Guid SamId    = Guid.Parse("b4d3c2e1-f0a9-4b33-c8e5-3e4f5a6b7c80");

    private static readonly IReadOnlyList<Customer> SeedCustomers =
    [
        new Customer
        {
            CustomerId = AveryId,
            Name       = new PersonName { First = "Avery", Last = "Lindgren" },
            Status     = CustomerStatus.ACTIVE,
            Contact    = new Contact
            {
                Emails =
                [
                    new Email { EmailAddress = "avery.lindgren@example.com", Type = EmailType.PRIMARY },
                ],
                Phones =
                [
                    new Phone { Number = "+1-555-0100", Type = PhoneType.MOBILE },
                ],
                Addresses =
                [
                    new Address
                    {
                        Type       = AddressType.HOME,
                        Line1      = "100 Birch Lane",
                        City       = "Central",
                        State      = "WI",
                        PostalCode = "54000",
                    },
                ],
            },
        },
        new Customer
        {
            CustomerId = JordanId,
            Name       = new PersonName { First = "Jordan", Last = "Okafor" },
            Status     = CustomerStatus.ACTIVE,
            Contact    = new Contact
            {
                Emails =
                [
                    new Email { EmailAddress = "jordan.okafor@example.com", Type = EmailType.PRIMARY },
                ],
                Phones =
                [
                    new Phone { Number = "+1-555-0200", Type = PhoneType.MOBILE },
                ],
                Addresses =
                [
                    new Address
                    {
                        Type       = AddressType.HOME,
                        Line1      = "42 Maple Street",
                        City       = "Lakeside",
                        State      = "WI",
                        PostalCode = "54001",
                    },
                ],
            },
        },
        new Customer
        {
            CustomerId = SamId,
            Name       = new PersonName { First = "Sam", Last = "Rivera" },
            Status     = CustomerStatus.INACTIVE,
            Contact    = new Contact
            {
                Emails =
                [
                    new Email { EmailAddress = "sam.rivera@example.com", Type = EmailType.PRIMARY },
                ],
                Phones =
                [
                    new Phone { Number = "+1-555-0300", Type = PhoneType.HOME },
                ],
                Addresses =
                [
                    new Address
                    {
                        Type       = AddressType.MAILING,
                        Line1      = "9 Elm Court",
                        City       = "Riverside",
                        State      = "WI",
                        PostalCode = "54002",
                    },
                ],
            },
        },
    ];

    public IReadOnlyList<Account> GetAccounts() => SeedAccounts;

    public IReadOnlyList<Transaction> GetTransactions(Guid accountId) =>
        SeedTransactions.Where(t => t.AccountId == accountId).ToList();

    public IReadOnlyList<Customer> GetCustomers() => SeedCustomers;
}
