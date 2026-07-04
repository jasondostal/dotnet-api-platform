namespace ApiPlatform.Platform.Results;

/// <summary>
/// Discriminant for the outcome of a vendor or upstream system call.
/// Used by <see cref="Result{T}"/> and propagated through
/// <see cref="ApiPlatform.Platform.Errors.UpstreamUnavailableException"/> to the host layer.
/// </summary>
public enum UpstreamOutcome
{
    /// <summary>The call succeeded and a value is available.</summary>
    Success,

    /// <summary>The requested resource does not exist at the upstream.</summary>
    NotFound,

    /// <summary>The upstream rejected the request due to insufficient authorization.</summary>
    Unauthorized,

    /// <summary>
    /// The upstream is temporarily unavailable or timed out.
    /// The call may be retried safely.
    /// </summary>
    Transient,

    /// <summary>
    /// The upstream returned a persistent, non-retryable error (e.g. 400 Bad Request, unhandled 5xx).
    /// </summary>
    VendorError,
}

/// <summary>
/// Allocation-light discriminated result for a vendor call that returns a value of type <typeparamref name="T"/>.
/// </summary>
/// <remarks>
/// Construct with the factory statics (<see cref="Success"/>, <see cref="NotFound"/>, etc.).
/// Inspect <see cref="Outcome"/> or <see cref="IsSuccess"/> to branch before accessing <see cref="Value"/>.
/// </remarks>
/// <typeparam name="T">Value type returned on a successful outcome.</typeparam>
public readonly struct Result<T>
{
    private readonly T? _value;

    private Result(UpstreamOutcome outcome, T? value, string? reason)
    {
        Outcome = outcome;
        _value  = value;
        Reason  = reason;
    }

    /// <summary>The outcome discriminant.</summary>
    public UpstreamOutcome Outcome { get; }

    /// <summary>
    /// The success value.
    /// Only valid when <see cref="IsSuccess"/> is <see langword="true"/>; throws
    /// <see cref="InvalidOperationException"/> for any other outcome.
    /// </summary>
    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException(
            $"Cannot access Value: result outcome is {Outcome}, not {UpstreamOutcome.Success}.");

    /// <summary>
    /// Human-readable reason text.
    /// Populated for <see cref="UpstreamOutcome.Transient"/> and <see cref="UpstreamOutcome.VendorError"/>
    /// outcomes; <see langword="null"/> for all others.
    /// </summary>
    public string? Reason { get; }

    /// <summary><see langword="true"/> when <see cref="Outcome"/> is <see cref="UpstreamOutcome.Success"/>.</summary>
    public bool IsSuccess => Outcome == UpstreamOutcome.Success;

    // ── Factory statics ──────────────────────────────────────────────────────────

    /// <summary>Creates a successful result carrying <paramref name="value"/>.</summary>
    public static Result<T> Success(T value) => new(UpstreamOutcome.Success, value, null);

    /// <summary>Creates a not-found result (the resource does not exist at the upstream).</summary>
    public static Result<T> NotFound() => new(UpstreamOutcome.NotFound, default, null);

    /// <summary>Creates an unauthorized result (the upstream rejected with an auth error).</summary>
    public static Result<T> Unauthorized() => new(UpstreamOutcome.Unauthorized, default, null);

    /// <summary>Creates a transient result (the upstream is temporarily unavailable or timed out).</summary>
    public static Result<T> Transient(string? reason = null) => new(UpstreamOutcome.Transient, default, reason);

    /// <summary>Creates a vendor-error result (the upstream returned a persistent error).</summary>
    public static Result<T> VendorError(string? reason = null) => new(UpstreamOutcome.VendorError, default, reason);
}
