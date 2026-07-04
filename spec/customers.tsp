import "@typespec/http";
import "@typespec/openapi";

using TypeSpec.Http;
using TypeSpec.OpenAPI;

namespace ApiPlatform;

// ── Enumerations ─────────────────────────────────────────────────────────────

/** Lifecycle status of a customer profile. */
enum CustomerStatus {
  ACTIVE,
  INACTIVE,
  CLOSED,
}

/** Classification of an email address. */
enum EmailType {
  PRIMARY,
  SECONDARY,
}

/** Classification of a phone number. */
enum PhoneType {
  MOBILE,
  HOME,
  WORK,
}

/** Classification of a postal address. */
enum AddressType {
  HOME,
  MAILING,
  WORK,
}

// ── Contact sub-models ────────────────────────────────────────────────────────

/** Legal name of a customer. */
model PersonName {
  first: string;
  last: string;
}

/** A customer email address. */
model Email {
  @format("email")
  email: string;

  type?: EmailType;
}

/** A customer phone number. */
model Phone {
  number: string;
  type?: PhoneType;
}

/** A customer postal address. */
model Address {
  type?: AddressType;
  line1: string;
  line2?: string;
  city: string;
  state: string;
  postalCode: string;
}

/**
 * Aggregated contact information for a customer.
 * Sensitive PII; returned only when the token carries `contact.read`.
 */
model Contact {
  emails?: Email[];
  phones?: Phone[];
  addresses?: Address[];
}

// ── Core models ───────────────────────────────────────────────────────────────

/**
 * A canonical customer (member) profile.
 * The `contact` object is gated behind `contact.read`.
 */
model Customer {
  /** Opaque customer identifier (UUID). */
  @format("uuid")
  customerId: string;

  name: PersonName;
  status: CustomerStatus;

  /** Contact info (PII); present only when token carries `contact.read`. */
  contact?: Contact;
}

/** Cursor-based page of customers. */
model CustomerList {
  data: Customer[];
  nextCursor?: string | null;
}

// ── Routes ────────────────────────────────────────────────────────────────────

/** Customer collection operations. */
@route("/v1/customers")
@tag("customers")
interface CustomersOps {
  /**
   * Returns customers the caller is entitled to.
   * Basic fields come with `customer.read`; the `contact` object requires
   * `contact.read` in addition.
   */
  @get
  @operationId("listCustomers")
  @summary("List customers")
  listCustomers(
    /** Opaque pagination cursor from a prior response's `nextCursor`. */
    @query cursor?: string,

    /** Maximum items per page (1–200, default 50). */
    @query @minValue(1) @maxValue(200) limit?: int32,
  ): OkResponse & Body<CustomerList> | BadRequest | Unauthorized | Forbidden;

  /**
   * Returns a single customer.
   * Includes the `contact` object when the token carries `contact.read`.
   */
  @get
  @route("{customerId}")
  @operationId("getCustomer")
  @summary("Get a customer")
  getCustomer(
    /** Opaque customer identifier (UUID). */
    @path @format("uuid") customerId: string,
  ): OkResponse & Body<Customer> | Unauthorized | Forbidden | NotFound;
}
