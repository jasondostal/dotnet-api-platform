using ApiPlatform.Contracts;
using ApiPlatform.Integration.Acl;
using ApiPlatform.Mcp.Mcp;
using ApiPlatform.Platform.Audit;
using ApiPlatform.Platform.Auth;
using ApiPlatform.Platform.Errors;
using ApiPlatform.Platform.Pii;

namespace ApiPlatform.Mcp;

/// <summary>
/// Registry of governed MCP-style tools. Each tool declares a required platform scope;
/// the toolset enforces scope, masks PII in results, and audits every call.
/// </summary>
public sealed class PlatformToolset
{
    private readonly IAccountSource  _accounts;
    private readonly ICustomerSource _customers;
    private readonly IPiiRedactor    _redactor;
    private readonly IPlatformAudit  _audit;

    private readonly IReadOnlyDictionary<string, GovernedTool> _tools;

    public PlatformToolset(
        IAccountSource  accounts,
        ICustomerSource customers,
        IPiiRedactor    redactor,
        IPlatformAudit  audit)
    {
        _accounts  = accounts;
        _customers = customers;
        _redactor  = redactor;
        _audit     = audit;
        _tools     = BuildRegistry();
    }

    // ── Public surface ────────────────────────────────────────────────────────

    /// <summary>
    /// Dispatches a tool call through the full governance pipeline:
    /// scope check → handler → PII masking → audit.
    /// </summary>
    public async Task<ToolResult> CallToolAsync(
        string toolName,
        IReadOnlyDictionary<string, object?> args,
        ToolCallContext ctx)
    {
        if (!_tools.TryGetValue(toolName, out var tool))
        {
            return ToolResult.Denied(
                ProblemTypes.NotFound,
                $"Tool '{toolName}' is not registered.");
        }

        // ── Scope gate ────────────────────────────────────────────────────────
        if (!ScopeCheck.HasScope(ctx.GrantedScopes, tool.RequiredScope))
        {
            await _audit.RecordAsync("tool.denied", new
            {
                Tool          = toolName,
                RequiredScope = tool.RequiredScope,
                Caller        = ctx.CallerId,
                Reason        = "insufficient_scope"
            }, ctx.CancellationToken);

            return ToolResult.Denied(
                ProblemTypes.Forbidden,
                $"Caller '{ctx.CallerId}' lacks required scope '{tool.RequiredScope}' for tool '{toolName}'.");
        }

        // ── Invoke handler (PII masking applied inside each handler) ──────────
        var content = await tool.Handler(args, ctx);

        await _audit.RecordAsync("tool.called", new
        {
            Tool    = toolName,
            Caller  = ctx.CallerId,
            Outcome = "success"
        }, ctx.CancellationToken);

        return ToolResult.Success(content);
    }

    // ── Tool registry ─────────────────────────────────────────────────────────

    private IReadOnlyDictionary<string, GovernedTool> BuildRegistry()
    {
        var tools = new List<GovernedTool>
        {
            new()
            {
                Name          = "accounts.list",
                RequiredScope = PlatformScopes.AccountRead,
                Description   = "Lists accounts visible to the caller. PII fields are masked.",
                Handler       = async (args, ctx) =>
                {
                    var cursor = args.TryGetValue("cursor", out var c) ? c?.ToString() : null;
                    var limit  = args.TryGetValue("limit",  out var l) && l is int li ? li : 20;
                    var list   = await _accounts.ListAccountsAsync(cursor, limit, ctx.CancellationToken);
                    return (object)MaskAccountList(list);
                }
            },
            new()
            {
                Name          = "customers.get",
                RequiredScope = PlatformScopes.CustomerRead,
                Description   = "Retrieves a single customer by ID. Contact PII is masked.",
                Handler       = async (args, ctx) =>
                {
                    var idStr = args.TryGetValue("customerId", out var v) ? v?.ToString() : null;
                    if (!Guid.TryParse(idStr, out var id))
                        throw new ArgumentException("customerId must be a valid GUID.");
                    var customer = await _customers.GetCustomerAsync(id, ctx.CancellationToken);
                    return customer is null ? (object)"not-found" : MaskCustomer(customer);
                }
            }
        };

        return tools.ToDictionary(t => t.Name, StringComparer.Ordinal);
    }

    // ── PII masking helpers ───────────────────────────────────────────────────

    private object MaskAccountList(AccountList list) => new
    {
        data       = list.Data.Select(MaskAccount).ToList(),
        nextCursor = list.NextCursor
    };

    private object MaskAccount(Account a) => new
    {
        accountId            = a.AccountId,
        accountType          = a.AccountType,
        // Account number display is PII — masked before leaving the toolset
        accountNumberDisplay = _redactor.Mask(a.AccountNumberDisplay),
        nickname             = _redactor.Mask(a.Nickname),
        status               = a.Status,
        currency             = a.Currency,
        productName          = a.ProductName
    };

    private object MaskCustomer(Customer c) => new
    {
        customerId = c.CustomerId,
        name       = c.Name,
        status     = c.Status,
        contact    = c.Contact is null ? null : MaskContact(c.Contact)
    };

    private object MaskContact(Contact contact) => new
    {
        emails = contact.Emails.Select(e => new
        {
            email = _redactor.MaskEmail(e.EmailAddress),
            type  = e.Type
        }).ToList(),
        phones = contact.Phones.Select(p => new
        {
            number = _redactor.MaskPhone(p.Number),
            type   = p.Type
        }).ToList(),
        addresses = contact.Addresses
    };
}
