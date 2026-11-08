using IdentityPlatform.Admin.Bff.Features.BrowserSessions;
using IdentityPlatform.Core.Features.Applications;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http.Features;

namespace IdentityPlatform.Admin.Bff.Features.Applications;

/// <summary>Uses the application-purpose native proof and original browser generation for one explicit assignment read or write.</summary>
internal static class ApplicationScopesProxy
{
    private const string Separator = "/";
    private const string Current = ".";
    private const string Parent = "..";
    private const string TransferEncoding = "Transfer-Encoding";
    private const string ContentEncoding = "Content-Encoding";

    /// <summary>Rechecks durable browser authority after a single send and suppresses stale output without pretending to roll back remote commits.</summary>
    internal static Task HandleAsync(HttpContext http, string id, bool write) => ApplicationMutationBoundary.RunAsync(http, async (budget, clock) =>
    {
        var mutation = await RequestAsync(http, id, write, clock);
        var state = await ApplicationBrowserState.ReadAsync(http, clock) ?? throw new ApplicationProxyRejected(StatusCodes.Status401Unauthorized);
        if (write)
        {
            http.Features.Set(state.Binding);
            await http.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(http);
        }
        budget.RequireCurrent();
        if (state.Expiry(clock.GetUtcNow()) is null)
        {
            throw new ApplicationProxyRejected(StatusCodes.Status401Unauthorized);
        }
        var result = await http.RequestServices.GetRequiredService<ApplicationProxyTransport>().ScopesAsync(state.Access, id, mutation, budget.Token);
        if (result.Output is not { } output)
        {
            throw new ApplicationProxyRejected(result.Status);
        }
        var current = await ApplicationBrowserState.ReadAsync(http, clock);
        var now = clock.GetUtcNow();
        if (current is null || !state.Same(current) || state.Expiry(now) is not { } original || current.Expiry(now) is not { } latest || output.ExpiresAt <= now)
        {
            throw new ApplicationProxyRejected(StatusCodes.Status401Unauthorized);
        }
        var narrowed = output with
        {
            ExpiresAt = new[] { original, latest, output.ExpiresAt }.Min()
        };
        http.Response.Headers.ETag = result.Version;
        await ApplicationMutationBoundary.WriteAsync(http, narrowed, result.Status, budget, () =>
            state.Expiry(clock.GetUtcNow()) is not null && current.Expiry(clock.GetUtcNow()) is not null && narrowed.ExpiresAt > clock.GetUtcNow());
    });

    /// <summary>Reuses strict mutation framing or requires an exact bodyless GET with same-origin provenance and no browser bearer forwarding.</summary>
    private static async Task<ApplicationMutationRequest?> RequestAsync(HttpContext http, string id, bool write, TimeProvider clock)
    {
        if (write)
        {
            return await ApplicationMutationRequest.ReadAsync(http, ApplicationProxyAction.AssignScopes, id, clock.GetUtcNow());
        }
        if (!ApplicationEditorRules.IsText(id, ApplicationProjection.IdentifierLength) || id is Current or Parent ||
            !ApplicationBrowserRequest.Origin(http, false) || !ApplicationBrowserRequest.Exact(http, ApplicationProxyEndpoint.Path + Separator + Uri.EscapeDataString(id) + ApplicationScopeRules.Suffix) ||
            !HttpMethods.IsGet(http.Request.Method) || http.Request.ContentLength is not (null or 0) ||
            http.Request.Headers.ContainsKey(TransferEncoding) || http.Request.Headers.ContainsKey(ContentEncoding) ||
            http.Features.Get<IHttpRequestBodyDetectionFeature>()?.CanHaveBody == true || !await BrowserLoginRequest.EmptyAsync(http))
        {
            throw new ApplicationProxyRejected(StatusCodes.Status400BadRequest);
        }
        return null;
    }
}
