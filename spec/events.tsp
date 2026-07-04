namespace ApiPlatform;

/**
 * Generic cloud event envelope.
 * Follows the CloudEvents 1.0 shape; `data` carries the domain-specific payload.
 */
model EventEnvelope<T> {
  /** Unique event identifier (UUID). */
  @format("uuid")
  id: string;

  /** Reverse-DNS event type (e.g. "northwind.account.touched"). */
  eventType: string;

  /** Resource path the event is about (e.g. "/accounts/{accountId}"). */
  subject: string;

  /** When the event was emitted. */
  time: utcDateTime;

  /** Domain-specific event payload. */
  data: T;
}

// ── Concrete payloads ─────────────────────────────────────────────────────────

/** Payload for account-touched events. */
model AccountTouchedPayload {
  /** The account that was touched. */
  @format("uuid")
  accountId: string;
}

/** Payload for record-created events. */
model RecordCreatedPayload {
  /** Type of the newly created resource (e.g. "account", "customer"). */
  resourceType: string;

  /** Identifier of the newly created resource. */
  @format("uuid")
  resourceId: string;
}

// ── Concrete event types ──────────────────────────────────────────────────────

/** An account-touched domain event (northwind.account.touched). */
model AccountTouchedEvent {
  @format("uuid")
  id: string;

  eventType: string;
  subject: string;
  time: utcDateTime;
  data: AccountTouchedPayload;
}

/** A record-created domain event (northwind.record.created). */
model RecordCreatedEvent {
  @format("uuid")
  id: string;

  eventType: string;
  subject: string;
  time: utcDateTime;
  data: RecordCreatedPayload;
}
