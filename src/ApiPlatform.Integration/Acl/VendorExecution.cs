using System.Net;
using ApiPlatform.Platform.Errors;
using ApiPlatform.Platform.Results;

namespace ApiPlatform.Integration.Acl;

/// <summary>
/// Shared ACL helper that executes a vendor delegate and classifies its outcome
/// into a <see cref="Result{T}"/>, then converts non-success outcomes to
/// <see cref="UpstreamUnavailableException"/> so they propagate honestly to the host.
/// </summary>
/// <remarks>
/// All vendor-call sites in the ACL boundary should use <see cref="ExecuteAsync{T}"/> so
/// that exception types are normalized consistently across every connector. The stubs in
/// this repo never make real network calls, but the classifier is exercised by throwing
/// the target exception types in tests.
/// </remarks>
internal static class VendorExecution
{
    /// <summary>
    /// Executes <paramref name="vendorCall"/> and classifies its outcome:
    /// <list type="bullet">
    ///   <item><see cref="TimeoutException"/> → <see cref="UpstreamOutcome.Transient"/></item>
    ///   <item><see cref="TaskCanceledException"/> not caused by the caller → <see cref="UpstreamOutcome.Transient"/></item>
    ///   <item><see cref="HttpRequestException"/> 401/403 → <see cref="UpstreamOutcome.Unauthorized"/></item>
    ///   <item><see cref="HttpRequestException"/> 404 → <see cref="UpstreamOutcome.NotFound"/></item>
    ///   <item><see cref="HttpRequestException"/> 429/502/503/504 → <see cref="UpstreamOutcome.Transient"/></item>
    ///   <item>Any other <see cref="HttpRequestException"/> or exception → <see cref="UpstreamOutcome.VendorError"/></item>
    ///   <item>Normal return → <see cref="UpstreamOutcome.Success"/></item>
    /// </list>
    /// Caller-initiated cancellation (<paramref name="ct"/> signalled) propagates as
    /// <see cref="OperationCanceledException"/> and is never classified as a vendor error.
    /// </summary>
    internal static async Task<Result<T>> ExecuteAsync<T>(
        Func<Task<T>> vendorCall,
        CancellationToken ct = default)
    {
        try
        {
            var value = await vendorCall().ConfigureAwait(false);
            return Result<T>.Success(value);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // The caller cancelled — propagate; do not swallow as a vendor error.
            throw;
        }
        catch (TaskCanceledException)
        {
            // TaskCanceledException that is NOT from the caller's token = vendor/network timeout.
            return Result<T>.Transient("Vendor request timed out.");
        }
        catch (TimeoutException ex)
        {
            return Result<T>.Transient(ex.Message);
        }
        catch (HttpRequestException ex)
        {
            return ClassifyHttpException<T>(ex);
        }
        catch (Exception ex)
        {
            return Result<T>.VendorError(ex.Message);
        }
    }

    /// <summary>
    /// Throws <see cref="UpstreamUnavailableException"/> if <paramref name="result"/> carries
    /// any outcome other than <see cref="UpstreamOutcome.Success"/> or <see cref="UpstreamOutcome.NotFound"/>.
    /// For those two outcomes the method is a no-op, allowing the caller to inspect the value or
    /// treat <see cref="UpstreamOutcome.NotFound"/> as <see langword="null"/> / empty.
    /// </summary>
    internal static void ThrowIfUpstreamError<T>(in Result<T> result, string vendorName)
    {
        if (result.Outcome is UpstreamOutcome.Success or UpstreamOutcome.NotFound)
            return;

        throw new UpstreamUnavailableException(result.Outcome, vendorName, result.Reason);
    }

    // ── Private helpers ───────────────────────────────────────────────────────────

    private static Result<T> ClassifyHttpException<T>(HttpRequestException ex) =>
        ex.StatusCode switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
                => Result<T>.Unauthorized(),
            HttpStatusCode.NotFound
                => Result<T>.NotFound(),
            HttpStatusCode.TooManyRequests
                or HttpStatusCode.BadGateway
                or HttpStatusCode.ServiceUnavailable
                or HttpStatusCode.GatewayTimeout
                => Result<T>.Transient(ex.Message),
            _ => Result<T>.VendorError(ex.Message),
        };
}
