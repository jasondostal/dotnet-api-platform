using System.Security.Claims;
using ApiPlatform.Integration.Acl;
using ApiPlatform.Api.Auth;
using ApiPlatform.Platform.Auth;
using ApiPlatform.Platform.Errors;
using Microsoft.AspNetCore.Mvc;

namespace ApiPlatform.Api.Endpoints;

public static class CustomerEndpoints
{
    public static IEndpointRouteBuilder MapCustomerEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/v1/customers")
            .WithTags("Customers")
            .WithOpenApi();

        group.MapGet("/", ListCustomers)
            .WithName("listCustomers")
            .WithSummary("List customers")
            .RequireAuthorization(Scopes.CustomerRead);

        group.MapGet("/{customerId:guid}", GetCustomer)
            .WithName("getCustomer")
            .WithSummary("Get a customer")
            .RequireAuthorization(Scopes.CustomerRead);

        return app;
    }

    private static async Task<IResult> ListCustomers(
        HttpContext ctx,
        ICustomerSource source,
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

        bool hasContact = user.HasClaim(PlatformScopes.ScopeClaimType, Scopes.ContactRead);

        var list = await source.ListCustomersAsync(cursor, limit, ctx.RequestAborted);

        if (!hasContact)
        {
            // Strip contact object — callers already received a clone, so mutation is safe
            foreach (var customer in list.Data)
            {
                customer.Contact = null;
            }
        }

        return Results.Ok(list);
    }

    private static async Task<IResult> GetCustomer(
        HttpContext ctx,
        ICustomerSource source,
        ClaimsPrincipal user,
        Guid customerId)
    {
        var customer = await source.GetCustomerAsync(customerId, ctx.RequestAborted);
        if (customer is null)
        {
            return Results.Problem(
                title: "Customer not found",
                detail: "No customer exists with the supplied identifier.",
                statusCode: ProblemTypes.NotFound.Status,
                type: ProblemTypes.NotFound.Type);
        }

        bool hasContact = user.HasClaim(PlatformScopes.ScopeClaimType, Scopes.ContactRead);
        if (!hasContact)
        {
            customer.Contact = null;
        }

        return Results.Ok(customer);
    }
}
