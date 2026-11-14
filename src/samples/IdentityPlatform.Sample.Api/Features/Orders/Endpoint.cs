using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http.Features;
using OpenIddict.Abstractions;
using OpenIddict.Validation.AspNetCore;

namespace IdentityPlatform.Sample.Api.Features.Orders;

/// <summary>Exposes a fixed synthetic resource whose authority comes only from native access-token validation and scope.</summary>
internal static class Endpoint
{
    private const string OrdersPath = "/orders";
    private const string TransferEncodingHeader = "Transfer-Encoding";
    private const int AuthorizationLimit = 16384;
    private const string SyntheticOrder = "sample-order-001";
    private const string ReadyStatus = "Ready";

    /// <summary>Maps the single protected example endpoint; it is not a management API or account data source.</summary>
    internal static void MapOrders(this IEndpointRouteBuilder endpoints) => endpoints.MapGet(OrdersPath, (Func<HttpContext, Task<IResult>>)ReadAsync);

    /// <summary>Rejects ambiguous transports, validates the native token, then requires the exact resource scope.</summary>
    private static async Task<IResult> ReadAsync(HttpContext context)
    {
        const string scheme = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
        var header = context.Request.Headers.Authorization;
        if (context.Features.Get<IHttpRequestBodyDetectionFeature>()?.CanHaveBody == true || context.Request.QueryString.HasValue || context.Request.ContentLength is > 0 || context.Request.Headers.ContainsKey(TransferEncodingHeader) ||
            header.Count != 1 || header[0] is not { Length: <= AuthorizationLimit } value || value.Contains(',', StringComparison.Ordinal))
        {
            return Results.Unauthorized();
        }
        var result = await context.AuthenticateAsync(scheme);
        if (!result.Succeeded || result.Principal is null)
        {
            return Results.Challenge(authenticationSchemes: [scheme]);
        }
        if (!result.Principal.HasScope(SampleProfile.ApiScope))
        {
            return Results.Forbid(authenticationSchemes: [scheme]);
        }
        return Results.Json(new
        {
            orders = new[] { new { id = SyntheticOrder, status = ReadyStatus } }
        });
    }
}
