using System.Text.Json.Serialization;

namespace ApiPlatform.Contracts;

// ── Customer models ───────────────────────────────────────────────────────────

[JsonConverter(typeof(JsonStringEnumConverter<CustomerStatus>))]
public enum CustomerStatus { ACTIVE, INACTIVE, CLOSED }

[JsonConverter(typeof(JsonStringEnumConverter<EmailType>))]
public enum EmailType { PRIMARY, SECONDARY }

[JsonConverter(typeof(JsonStringEnumConverter<PhoneType>))]
public enum PhoneType { MOBILE, HOME, WORK }

[JsonConverter(typeof(JsonStringEnumConverter<AddressType>))]
public enum AddressType { HOME, MAILING, WORK }

public class PersonName
{
    public string First { get; set; } = string.Empty;
    public string Last  { get; set; } = string.Empty;
}

public class Email
{
    [System.Text.Json.Serialization.JsonPropertyName("email")]
    public string    EmailAddress { get; set; } = string.Empty;
    public EmailType? Type        { get; set; }
}

public class Phone
{
    public string     Number { get; set; } = string.Empty;
    public PhoneType? Type   { get; set; }
}

public class Address
{
    public AddressType? Type       { get; set; }
    public string       Line1      { get; set; } = string.Empty;
    public string?      Line2      { get; set; }
    public string       City       { get; set; } = string.Empty;
    public string       State      { get; set; } = string.Empty;
    public string       PostalCode { get; set; } = string.Empty;
}

public class Contact
{
    public List<Email>   Emails    { get; set; } = [];
    public List<Phone>   Phones    { get; set; } = [];
    public List<Address> Addresses { get; set; } = [];
}

public class Customer
{
    public Guid         CustomerId { get; set; }
    public PersonName   Name       { get; set; } = new();
    public CustomerStatus Status   { get; set; }
    // Populated only when the token carries contact.read
    public Contact?     Contact    { get; set; }
}

public class CustomerList
{
    public List<Customer> Data       { get; set; } = [];
    public string?        NextCursor { get; set; }
}

// ── Account models ────────────────────────────────────────────────────────────

[JsonConverter(typeof(JsonStringEnumConverter<AccountType>))]
public enum AccountType { DEPOSIT, CREDIT, LOAN }

[JsonConverter(typeof(JsonStringEnumConverter<AccountStatus>))]
public enum AccountStatus { OPEN, CLOSED, FROZEN }

[JsonConverter(typeof(JsonStringEnumConverter<TransactionType>))]
public enum TransactionType { DEBIT, CREDIT }

[JsonConverter(typeof(JsonStringEnumConverter<TransactionStatus>))]
public enum TransactionStatus { PENDING, POSTED }

public class Account
{
    public Guid AccountId { get; set; }
    public AccountType AccountType { get; set; }
    public string? AccountNumberDisplay { get; set; }
    public string? Nickname { get; set; }
    public AccountStatus Status { get; set; }
    public string Currency { get; set; } = "USD";
    public string? ProductName { get; set; }

    // Type-specific detail objects — only populated when account.detailed.read scope is present
    public DepositDetail? DepositAccount { get; set; }
    public CreditDetail? CreditAccount { get; set; }
    public LoanDetail? LoanAccount { get; set; }
}

public class DepositDetail
{
    public decimal CurrentBalance { get; set; }
    public decimal AvailableBalance { get; set; }
}

public class CreditDetail
{
    public decimal CreditLimit { get; set; }
    public decimal CurrentBalance { get; set; }
    public decimal AvailableCredit { get; set; }
    public decimal? PurchaseApr { get; set; }
    public decimal? MinimumPaymentDue { get; set; }
    public DateOnly? PaymentDueDate { get; set; }
}

public class LoanDetail
{
    public decimal PrincipalBalance { get; set; }
    public decimal InterestRate { get; set; }
    public decimal PaymentAmount { get; set; }
    public DateOnly? NextPaymentDate { get; set; }
}

public class AccountList
{
    public List<Account> Data { get; set; } = [];
    public string? NextCursor { get; set; }
}

public class Transaction
{
    public Guid TransactionId { get; set; }
    public Guid AccountId { get; set; }
    public TransactionType TransactionType { get; set; }
    public decimal Amount { get; set; }
    public string? Description { get; set; }
    public TransactionStatus Status { get; set; }
    public DateOnly? PostedDate { get; set; }
    public DateOnly? TransactionDate { get; set; }
    public string? MerchantName { get; set; }
    public string? MerchantCategoryCode { get; set; }
}

public class TransactionList
{
    public List<Transaction> Data { get; set; } = [];
    public string? NextCursor { get; set; }
}
