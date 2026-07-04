import "@typespec/http";

using TypeSpec.Http;

namespace ApiPlatform;

/** RFC 9457 Problem Details for HTTP APIs. */
model ProblemDetails {
  /** URI reference identifying the problem type. */
  type?: url;

  /** Short, human-readable summary of the problem. */
  title: string;

  /** HTTP status code. */
  status: int32;

  /** Human-readable explanation of this specific occurrence. */
  detail?: string;

  /** URI reference identifying this specific occurrence of the problem. */
  instance?: url;
}

/**
 * A response envelope that carries Problem Details with a specific HTTP status
 * and `application/problem+json` content type (RFC 9457).
 */
model ProblemResponse<Status extends int32> {
  @statusCode statusCode: Status;
  @header contentType: "application/problem+json";
  @body body: ProblemDetails;
}

/** 400 Bad Request — malformed or invalid request. */
alias BadRequest = ProblemResponse<400>;

/** 401 Unauthorized — missing or invalid bearer token. */
alias Unauthorized = ProblemResponse<401>;

/**
 * 403 Forbidden — token lacks the required scope.
 * ProblemType: Forbidden.
 */
alias Forbidden = ProblemResponse<403>;

/**
 * 404 Not Found — resource does not exist or is not visible to the caller.
 * ProblemType: NotFound.
 */
alias NotFound = ProblemResponse<404>;

/**
 * 409 Conflict — state conflict (e.g. duplicate resource).
 * ProblemType: Conflict.
 */
alias Conflict = ProblemResponse<409>;

/**
 * 422 Unprocessable Entity — semantic validation failure.
 * ProblemType: Validation.
 */
alias ValidationError = ProblemResponse<422>;

/**
 * 409 Conflict — idempotency key reuse with different payload.
 * ProblemType: Idempotency.
 */
alias IdempotencyConflict = ProblemResponse<409>;
