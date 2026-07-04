using System.Security.Claims;
using ApiPlatform.Integration.Acl;
using ApiPlatform.Api.Auth;
using ApiPlatform.Contracts;
using ApiPlatform.Api.Eventing;
using ApiPlatform.Api.Formatters;
using ApiPlatform.Platform.Auth;
using ApiPlatform.Platform.Errors;
using ApiPlatform.Platform.Pii;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace ApiPlatform.Api.Endpoints;

public static class AccountEndpoints
{
    public static IEndpointRouteBuilder MapAccountEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/v1/accounts")
            .WithTags("Accounts")
            .WithOpenApi();

        group.MapGet("/", ListAccounts)
            .WithName("listAccounts")
            .WithSummary("List accounts")
            .RequireAuthorization(Scopes.AccountRead);

        group.MapGet("/{accountId:guid}", GetAccount)
            .WithName("getAccount")
            .WithSummary("Get an account")
            .RequireAuthorization(Scopes.AccountRead);

        group.MapGet("/{accountId:guid}/transactions", ListTransactions)
            .WithName("listAccountTransactions")
            .WithSummary("List transactions for an account")
            .RequireAuthorization(Scopes.TransactionRead);

        group.MapPost("/{accountId:guid}/touch", TouchAccount)
            .WithName("touchAccount")
            .WithSummary("Emit an account-touched event")
            .RequireAuthorization(Scopes.EventPublish);

        return app;
    }

    private static async Task<IResult> ListAccounts(
        HttpContext ctx,
        IAccountSource source,
        ClaimsPrincipal user,
        [FromQuery] string? cursor = null,
        [FromQuery] int limit = 50)
    {
        if (limit is < 1 or > 200)
        {
            return Results.Problem(
                title: ProblemTypes.InvalidParameter.Title,
                detail: "limit must be between 1 and 200.",
                statusCode: ProblemTypes.InvalidParameter.Status,
                type: ProblemTypes.InvalidParameter.Type);
        }

        bool hasDetailed = user.HasClaim(PlatformScopes.ScopeClaimType, Scopes.AccountDetailedRead);

        var list = await source.ListAccountsAsync(cursor, limit, ctx.RequestAborted);

        if (!hasDetailed)
        {
            // Strip type-specific detail objects
            foreach (var account in list.Data)
            {
                account.DepositAccount = null;
                account.CreditAccount  = null;
                account.LoanAccount    = null;
            }
        }

        if (CsvOutputFormatter.RequestedCsv(ctx.Request))
        {
            await CsvOutputFormatter.WriteCsvAsync(ctx, list.Data);
            return Results.Empty;
        }

        return Results.Ok(list);
    }

    private static async Task<IResult> GetAccount(
        HttpContext ctx,
        IAccountSource source,
        ClaimsPrincipal user,
        Guid accountId)
    {
        var account = await source.GetAccountAsync(accountId, ctx.RequestAborted);
        if (account is null)
        {
            return Results.Problem(
                title: "Account not found",
                detail: "No account exists with the supplied identifier.",
                statusCode: ProblemTypes.NotFound.Status,
                type: ProblemTypes.NotFound.Type);
        }

        bool hasDetailed = user.HasClaim(PlatformScopes.ScopeClaimType, Scopes.AccountDetailedRead);
        if (!hasDetailed)
        {
            account.DepositAccount = null;
            account.CreditAccount  = null;
            account.LoanAccount    = null;
        }

        return Results.Ok(account);
    }

    private static async Task<IResult> ListTransactions(
        HttpContext ctx,
        IAccountSource source,
        Guid accountId,
        [FromQuery] string? cursor = null,
        [FromQuery] int limit = 50)
    {
        if (limit is < 1 or > 200)
        {
            return Results.Problem(
                title: ProblemTypes.InvalidParameter.Title,
                detail: "limit must be between 1 and 200.",
                statusCode: ProblemTypes.InvalidParameter.Status,
                type: ProblemTypes.InvalidParameter.Type);
        }

        // Verify account exists
        var account = await source.GetAccountAsync(accountId, ctx.RequestAborted);
        if (account is null)
        {
            return Results.Problem(
                title: "Account not found",
                detail: "No account exists with the supplied identifier.",
                statusCode: ProblemTypes.NotFound.Status,
                type: ProblemTypes.NotFound.Type);
        }

        var list = await source.ListTransactionsAsync(accountId, cursor, limit, ctx.RequestAborted);

        if (CsvOutputFormatter.RequestedCsv(ctx.Request))
        {
            await CsvOutputFormatter.WriteCsvAsync(ctx, list.Data);
            return Results.Empty;
        }

        return Results.Ok(list);
    }

    private static async Task<IResult> TouchAccount(
        HttpContext    ctx,
        IAccountSource source,
        IEventPublisher publisher,
        IPiiRedactor   redactor,
        Guid           accountId)
    {
        var account = await source.GetAccountAsync(accountId, ctx.RequestAborted);
        if (account is null)
        {
            return Results.Problem(
                title: "Account not found",
                detail: "No account exists with the supplied identifier.",
                statusCode: ProblemTypes.NotFound.Status,
                type: ProblemTypes.NotFound.Type);
        }

        // Fire-and-forget publish — never fails the request
        await publisher.PublishAccountTouchedAsync(accountId, ctx.RequestAborted);

        // Mask the account id before it reaches the audit store — the audit trail records
        // that a touch happened (operation label stays legible) without storing the raw id.
        Audit.Core.AuditScope.Log("Account:Touched", new { accountId = redactor.Mask(accountId.ToString()) });

        return Results.Accepted(value: new
        {
            eventType = "northwind.account.touched",
            id        = accountId.ToString(),
            status    = "PUBLISHED",
        });
    }
}
