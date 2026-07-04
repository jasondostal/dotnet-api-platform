namespace ApiPlatform.Platform.Errors;

/// <summary>
/// Immutable RFC 9457 problem type descriptor.
/// </summary>
public sealed record ProblemType(string Type, string Title, int Status);

/// <summary>
/// Catalog of RFC 9457 problem types used across the platform.
/// URI pattern: https://apiplatform.dev/problems/{slug}
/// </summary>
public static class ProblemTypes
{
    public static readonly ProblemType Forbidden = new(
        "https://apiplatform.dev/problems/forbidden",
        "Forbidden",
        403);

    public static readonly ProblemType NotFound = new(
        "https://apiplatform.dev/problems/not-found",
        "Not Found",
        404);

    public static readonly ProblemType Validation = new(
        "https://apiplatform.dev/problems/validation",
        "Validation Error",
        400);

    public static readonly ProblemType Conflict = new(
        "https://apiplatform.dev/problems/conflict",
        "Conflict",
        409);

    public static readonly ProblemType Idempotency = new(
        "https://apiplatform.dev/problems/idempotency",
        "Idempotency Conflict",
        409);

    public static readonly ProblemType Unauthorized = new(
        "https://apiplatform.dev/problems/unauthorized",
        "Unauthorized",
        401);

    public static readonly ProblemType InvalidParameter = new(
        "https://apiplatform.dev/problems/invalid-parameter",
        "Invalid Request Parameter",
        400);

    public static readonly ProblemType UpstreamUnavailable = new(
        "https://apiplatform.dev/problems/upstream-unavailable",
        "Upstream Unavailable",
        502);
}
