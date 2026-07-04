namespace ApiPlatform;

/** Type of change represented by a feed entry. */
enum ChangeType {
  created,
  updated,
  deleted,
}

/**
 * A single entry in a resource change feed (SSE / long-poll shape).
 * `T` is the snapshot of the resource after the change.
 */
model ChangeFeedEntry<T> {
  /** Monotonically increasing sequence number within the feed. */
  sequence: int64;

  type: ChangeType;

  /** When the change occurred. */
  timestamp: utcDateTime;

  /** Resource snapshot after the change (absent for `deleted`). */
  payload?: T;
}

/**
 * A page of change-feed entries returned by a polling endpoint.
 * Pass `lastEventId` as the cursor on the next poll.
 */
model ChangeFeedPage<T> {
  entries: ChangeFeedEntry<T>[];

  /** Opaque cursor; pass on the next request to continue from this position. */
  lastEventId?: string;
}
