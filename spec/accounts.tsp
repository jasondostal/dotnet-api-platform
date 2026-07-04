import "@typespec/http";
import "@typespec/openapi";

using TypeSpec.Http;
using TypeSpec.OpenAPI;

namespace ApiPlatform;

// ── Enumerations ─────────────────────────────────────────────────────────────

/** Category of account as modelled by the core platform. */
enum AccountType {
  DEPOSIT,
  CREDIT,
  LOAN,
}

/** Lifecycle status of an account. */
enum AccountStatus {
  OPEN,
  CLOSED,
  FROZEN,
}

/** Direction of a transaction relative to the account. */
enum TransactionType {
  DEBIT,
  CREDIT,
}

/** Settlement status of a transaction. */
enum TransactionStatus {
  PENDING,
  POSTED,
}

// ── Account detail objects ────────────────────────────────────────────────────

/**
 * Balance fields for deposit-type accounts (share, savings, checking).
 * Present only when the token carries `account.detailed.read`.
 */
model DepositDetail {
  currentBalance?: float64;
  availableBalance?: float64;
}

/**
 * Balance and rate fields for revolving credit accounts.
 * Present only when the token carries `account.detailed.read`.
 */
model CreditDetail {
  creditLimit?: float64;
  currentBalance?: float64;
  availableCredit?: float64;
  purchaseApr?: float64;
  minimumPaymentDue?: float64;
  paymentDueDate?: plainDate;
}

/**
 * Balance and payment fields for instalment loan accounts.
 * Present only when the token carries `account.detailed.read`.
 */
model LoanDetail {
  principalBalance?: float64;
  interestRate?: float64;
  paymentAmount?: float64;
  nextPaymentDate?: plainDate;
}

// ── Core models ───────────────────────────────────────────────────────────────

/**
 * A canonical account. `accountType` selects which (if any) type-specific
 * detail object is present. Detail objects require `account.detailed.read`.
 */
model Account {
  /** Opaque identifier. Never a real account number. */
  @format("uuid")
  accountId: string;

  accountType: AccountType;

  /** Masked display value, e.g. "****1234". */
  accountNumberDisplay?: string;

  nickname?: string;
  status: AccountStatus;

  /** ISO 4217 currency code. */
  currency: string;

  productName?: string;

  /** Present when `accountType` is DEPOSIT and token carries `account.detailed.read`. */
  depositAccount?: DepositDetail;

  /** Present when `accountType` is CREDIT and token carries `account.detailed.read`. */
  creditAccount?: CreditDetail;

  /** Present when `accountType` is LOAN and token carries `account.detailed.read`. */
  loanAccount?: LoanDetail;
}

/** Cursor-based page of accounts. */
model AccountList {
  data: Account[];
  nextCursor?: string | null;
}

/** A posted or pending transaction on an account. */
model Transaction {
  @format("uuid")
  transactionId: string;

  @format("uuid")
  accountId: string;

  transactionType: TransactionType;
  amount: float64;
  description?: string;
  status: TransactionStatus;
  postedDate?: plainDate;
  transactionDate?: plainDate;
  merchantName?: string;
  merchantCategoryCode?: string;
}

/** Cursor-based page of transactions. */
model TransactionList {
  data: Transaction[];
  nextCursor?: string | null;
}

/** Acknowledgement returned after an account event is accepted for publication. */
model EventAccepted {
  eventType: string;

  @format("uuid")
  id: string;

  status: "PUBLISHED";
}

// ── Routes ────────────────────────────────────────────────────────────────────

/** Account collection operations. */
@route("/v1/accounts")
@tag("accounts")
interface AccountsOps {
  /**
   * Returns the accounts the caller is entitled to.
   * Basic fields come with `account.read`; type-specific detail objects
   * require `account.detailed.read` in addition.
   */
  @get
  @operationId("listAccounts")
  @summary("List accounts")
  listAccounts(
    /** Opaque pagination cursor from a prior response's `nextCursor`. */
    @query cursor?: string,

    /** Maximum items per page (1–200, default 50). */
    @query @minValue(1) @maxValue(200) limit?: int32,
  ): OkResponse & Body<AccountList> | BadRequest | Unauthorized | Forbidden;

  /**
   * Returns a single account.
   * Includes the type-specific detail object when the token carries
   * `account.detailed.read`.
   */
  @get
  @route("{accountId}")
  @operationId("getAccount")
  @summary("Get an account")
  getAccount(
    /** Opaque account identifier (UUID). */
    @path @format("uuid") accountId: string,
  ): OkResponse & Body<Account> | Unauthorized | Forbidden | NotFound;
}

/** Transaction and event operations scoped to a specific account. */
@route("/v1/accounts/{accountId}")
interface AccountSubresourceOps {
  /**
   * Returns a page of posted and pending transactions for the account.
   */
  @get
  @route("transactions")
  @operationId("listAccountTransactions")
  @summary("List transactions for an account")
  @tag("transactions")
  listAccountTransactions(
    /** Opaque account identifier (UUID). */
    @path @format("uuid") accountId: string,

    /** Opaque pagination cursor from a prior response's `nextCursor`. */
    @query cursor?: string,

    /** Maximum items per page (1–200, default 50). */
    @query @minValue(1) @maxValue(200) limit?: int32,
  ): OkResponse & Body<TransactionList> | Unauthorized | Forbidden | NotFound;

  /**
   * Publishes a minimal domain event (`account.touched`) carrying only the
   * account identifier. Event Grid fans it out to a webhook and to
   * per-consumer queues. No request body; returns 202.
   */
  @post
  @route("touch")
  @operationId("touchAccount")
  @summary("Emit an account-touched event")
  @tag("accounts")
  touchAccount(
    /** Opaque account identifier (UUID). */
    @path @format("uuid") accountId: string,
  ): AcceptedResponse & Body<EventAccepted> | Unauthorized | Forbidden | NotFound;
}
