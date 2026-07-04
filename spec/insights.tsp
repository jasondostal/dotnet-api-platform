namespace ApiPlatform;

/**
 * A single metric or insight data point.
 * Stub model for analytics store integration (future domain).
 */
model Insight {
  /** Opaque identifier (UUID). */
  @format("uuid")
  id: string;

  /** Metric name (e.g. "daily_active_accounts"). */
  metricName: string;

  /** Numeric metric value. */
  value: float64;

  /** Unit of measure (e.g. "count", "USD", "percent"). */
  unit?: string;

  /** Time period the metric covers (e.g. "2026-06", "2026-Q2"). */
  period?: string;

  /** When the metric was computed or observed. */
  timestamp?: utcDateTime;
}

/** A collection of insight rows returned by a query. */
model InsightList {
  data: Insight[];
  nextCursor?: string | null;
}
