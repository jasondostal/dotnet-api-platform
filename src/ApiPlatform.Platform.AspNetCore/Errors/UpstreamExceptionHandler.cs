using ApiPlatform.Platform.Errors;
using ApiPlatform.Platform.Results;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ApiPlatform.Platform.AspNetCore.Errors;

/// <summary>
/// Maps <see cref="UpstreamUnavailableException"/> to RFC 9457 problem details at the HTTP edge.
/// Registered via <c>services.AddExceptionHandler&lt;UpstreamExceptionHandler&gt;()</c> and
/// invoked by the platform's <c>UseExceptionHandler()</c> middleware before the generic fallback.
/// </summary>
/// <remarks>
/// Outcome mapping:
/// <list type="bullet">
///   <item><see cref="UpstreamOutcome.Transient"/> → HTTP 503 (retryable)</item>
///   <item><see cref="UpstreamOutcome.VendorError"/> → HTTP 502</item>
///   <item><see cref="UpstreamOutcome.Unauthorized"/> → HTTP 502 (never surfaces as caller 401)</item>
/// </list>
/// All outcomes use the <see cref="ProblemTypes.UpstreamUnavailable"/> type URI so clients
/// can reliably distinguish upstream faults from local validation or auth errors.
/// </remarks>
internal sealed class UpstreamExceptionHandler(IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not UpstreamUnavailableException upstreamEx)
            return false;

        // Transient (e.g. timeout, 503 from vendor) → 503 to hint retryability.
        // VendorError and Unauthorized → 502 (vendor responded but incorrectly/badly).
        // An upstream 401 MUST NOT surface as the caller's 401.
        var statusCode = upstreamEx.Outcome == UpstreamOutcome.Transient ? 503 : 502;

        httpContext.Response.StatusCode = statusCode;

        await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext    = httpContext,
            Exception      = exception,
            ProblemDetails = new ProblemDetails
            {
                Type     = ProblemTypes.UpstreamUnavailable.Type,
                Title    = ProblemTypes.UpstreamUnavailable.Title,
                Status   = statusCode,
                Detail   = "An upstream service is temporarily unavailable. Try again later.",
                Instance = httpContext.Request.Path,
            },
        });

        return true;
    }
}
