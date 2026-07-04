import "@typespec/http";

using TypeSpec.Http;

namespace ApiPlatform;

/**
 * Generic cursor-based page envelope used by all list endpoints.
 * Errors are RFC 9457 Problem Details; see errors.tsp.
 */
model PageOf<T> {
  /** Items for this page. */
  data: T[];

  /**
   * Opaque cursor to pass as `cursor` on the next request.
   * Null when this is the last page.
   */
  nextCursor?: string | null;
}
