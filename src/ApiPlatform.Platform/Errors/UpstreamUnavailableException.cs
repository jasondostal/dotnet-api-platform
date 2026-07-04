using ApiPlatform.Platform.Results;

namespace ApiPlatform.Platform.Errors;

/// <summary>
/// Thrown at the ACL boundary when a vendor call produces a non-success, non-not-found outcome.
/// Carries the classified <see cref="UpstreamOutcome"/> so the host exception handler can map
/// it to an RFC 9457 problem response (502 Bad Gateway or 503 Service Unavailable) without
/// surfacing the vendor's own error semantics to callers.
/// </summary>
/// <remarks>
/// An upstream <see cref="UpstreamOutcome.Unauthorized"/> MUST be mapped to 502, never 401 —
/// the caller's authorization was accepted; it is the vendor-to-platform call that failed.
/// </remarks>
public sealed class UpstreamUnavailableException : Exception
{
    /// <summary>The classified outcome of the failed vendor call.</summary>
    public UpstreamOutcome Outcome { get; }

    /// <summary>The name of the vendor system that failed, if known.</summary>
    public string? VendorName { get; }

    /// <summary>
    /// Initializes a new instance with the vendor outcome, optional vendor name, and optional reason.
    /// </summary>
    /// <param name="outcome">The classified failure outcome. Must not be <see cref="UpstreamOutcome.Success"/>.</param>
    /// <param name="vendorName">Human-readable vendor system name (e.g. "Core Banking").</param>
    /// <param name="reason">Optional detail from the underlying exception or vendor response.</param>
    public UpstreamUnavailableException(UpstreamOutcome outcome, string? vendorName = null, string? reason = null)
        : base(FormatMessage(outcome, vendorName, reason))
    {
        Outcome    = outcome;
        VendorName = vendorName;
    }

    private static string FormatMessage(UpstreamOutcome outcome, string? vendorName, string? reason)
    {
        var vendor = vendorName is not null ? $" '{vendorName}'" : string.Empty;
        var detail = reason is not null ? $": {reason}" : string.Empty;
        return $"Upstream vendor{vendor} returned outcome {outcome}{detail}.";
    }
}
