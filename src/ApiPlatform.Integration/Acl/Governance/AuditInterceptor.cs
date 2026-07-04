using System.Diagnostics;
using System.Reflection;
using ApiPlatform.Platform.Audit;
using ApiPlatform.Platform.Diagnostics;
using ApiPlatform.Platform.Pii;
using Castle.DynamicProxy;

namespace ApiPlatform.Integration.Acl.Governance;

/// <summary>
/// The ONE governance interceptor. It wraps every asynchronous operation on every vendor source —
/// reads and writes alike (List/Get/Create/Renew/ChangeValue/…) — opening an OpenTelemetry activity
/// (the observability face) and writing a compliance <see cref="AccessAuditRecord"/> (the audit
/// face) around the call, joined by the trace id. A new vendor source inherits all of this for free:
/// the developer writes the adapter, writes zero audit code, and every operation is audited.
/// Synchronous members (e.g. metadata property getters) pass through untouched.
/// </summary>
internal sealed class AuditInterceptor : IAsyncInterceptor
{
    private readonly IPlatformAudit _audit;
    private readonly IAuditContext _context;
    private readonly IPiiRedactor _redactor;
    private readonly TimeProvider _timeProvider;

    public AuditInterceptor(IPlatformAudit audit, IAuditContext context, IPiiRedactor redactor, TimeProvider timeProvider)
    {
        _audit = audit;
        _context = context;
        _redactor = redactor;
        _timeProvider = timeProvider;
    }

    // Property getters / other synchronous members are metadata, not operations — pass through.
    public void InterceptSynchronous(IInvocation invocation) => invocation.Proceed();

    public void InterceptAsynchronous(IInvocation invocation)
        => invocation.ReturnValue = GovernVoidAsync(invocation, invocation.CaptureProceedInfo());

    public void InterceptAsynchronous<TResult>(IInvocation invocation)
        => invocation.ReturnValue = GovernResultAsync<TResult>(invocation, invocation.CaptureProceedInfo());

    private async Task GovernVoidAsync(IInvocation invocation, IInvocationProceedInfo proceedInfo)
    {
        var (resource, operation, inputs) = Describe(invocation);
        using var activity = StartActivity(resource, operation, out var traceId);
        try
        {
            proceedInfo.Invoke();
            await ((Task)invocation.ReturnValue!).ConfigureAwait(false);
            await RecordAsync(resource, operation, inputs, "allowed", traceId).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            await RecordAsync(resource, operation, inputs, "error", traceId).ConfigureAwait(false);
            throw;
        }
    }

    private async Task<TResult> GovernResultAsync<TResult>(IInvocation invocation, IInvocationProceedInfo proceedInfo)
    {
        var (resource, operation, inputs) = Describe(invocation);
        using var activity = StartActivity(resource, operation, out var traceId);
        try
        {
            proceedInfo.Invoke();
            var result = await ((Task<TResult>)invocation.ReturnValue!).ConfigureAwait(false);
            await RecordAsync(resource, operation, inputs, "allowed", traceId).ConfigureAwait(false);
            return result;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            await RecordAsync(resource, operation, inputs, "error", traceId).ConfigureAwait(false);
            throw;
        }
    }

    private static Activity? StartActivity(string resource, string operation, out string? traceId)
    {
        var activity = PlatformDiagnostics.ActivitySource.StartActivity($"{resource}.{operation}");
        activity?.SetTag("platform.resource", resource);
        activity?.SetTag("platform.operation", operation);
        traceId = (activity?.TraceId ?? Activity.Current?.TraceId)?.ToString();
        return activity;
    }

    private Task RecordAsync(string resource, string operation, string inputs, string outcome, string? traceId)
        => _audit.RecordAsync(
            $"{resource}.{operation}",
            new AccessAuditRecord(_context.Actor, operation, resource, inputs, outcome, traceId, _timeProvider.GetUtcNow()));

    private (string Resource, string Operation, string Inputs) Describe(IInvocation invocation)
    {
        var resource = invocation.Method.DeclaringType?.Name ?? "Source";
        var operation = invocation.Method.Name;
        var parameters = invocation.Method.GetParameters();
        var inputs = string.Join(", ", invocation.Arguments
            .Select((arg, i) => MaskArgument(arg, i < parameters.Length ? parameters[i] : null)));
        return (resource, operation, inputs);
    }

    // Classifies and masks a single method argument by its parameter name (case-insensitive).
    //
    // SENSITIVE NAMES — masked via IPiiRedactor: any parameter whose name contains one of
    //   id, account, member, customer, ssn, tax, number, phone, email, name, dob, birth.
    //   For a Guid, the full value is suppressed (→ "***"). For a string, the appropriate
    //   redactor method is selected (email → MaskEmail, phone → MaskPhone, else → Mask).
    //
    // NON-SENSITIVE — kept legible: cursor, limit, offset, page, pageSize, count, and any
    //   CancellationToken. Boolean, numeric, date, and enum scalars are always legible so the
    //   audit trail remains useful for operational review.
    //
    // DEFENSE IN DEPTH: a string containing '@' is always routed through MaskEmail regardless of
    //   the parameter name.
    //
    // COMPLEX OBJECTS: recorded by type name only — payload structs are never dumped.
    private string MaskArgument(object? argument, ParameterInfo? param)
    {
        if (argument is CancellationToken) return "ct";
        if (argument is null) return "null";

        var paramName = param?.Name ?? "";
        bool isSensitive = IsSensitiveParamName(paramName);

        return argument switch
        {
            // Guid: fully suppress sensitive id parameters; non-sensitive Guids shown in full (rare in seams)
            Guid g when isSensitive => _redactor.Mask(g.ToString()),
            Guid g => g.ToString(),

            // Strings: email addresses masked by content regardless of name (defense in depth),
            // then phone-named parameters, then any other sensitive name, then legible pass-through
            string s when s.Contains('@') => _redactor.MaskEmail(s),
            string s when isSensitive && IsPhoneParamName(paramName) => _redactor.MaskPhone(s),
            string s when isSensitive => _redactor.Mask(s),
            string s => s,

            // Operational metadata: booleans, numeric scalars, date types, and enum flags stay
            // legible so audit records remain useful for operational analysis
            bool or int or long or decimal or double or DateTime or DateTimeOffset or DateOnly or Enum
                => argument.ToString() ?? "",

            // Complex objects: type name only — never dump PII-bearing payload fields
            _ => argument.GetType().Name,
        };
    }

    // Returns true when the parameter name suggests an identifier or personally-identifiable field.
    // Substring-match is intentional: "accountId", "customerId", "taxId", "phoneNumber", etc. all match.
    // When in doubt, prefer masking (fail safe toward privacy).
    private static bool IsSensitiveParamName(string name)
    {
        if (name.Length == 0) return false;
        var lower = name.ToLowerInvariant();
        return lower.Contains("id")
            || lower.Contains("account")
            || lower.Contains("member")
            || lower.Contains("customer")
            || lower.Contains("ssn")
            || lower.Contains("tax")
            || lower.Contains("number")
            || lower.Contains("phone")
            || lower.Contains("email")
            || lower.Contains("name")
            || lower.Contains("dob")
            || lower.Contains("birth");
    }

    // Returns true when the parameter name specifically suggests a telephone number field.
    private static bool IsPhoneParamName(string name)
    {
        var lower = name.ToLowerInvariant();
        return lower.Contains("phone") || lower.Contains("mobile") || lower.Contains("tel");
    }
}
