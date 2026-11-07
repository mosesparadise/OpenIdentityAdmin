using System.Text.Json;
using IdentityPlatform.Admin.Api.Features.Applications;
using IdentityPlatform.Admin.Api.Features.Authentication;
using IdentityPlatform.Core.Features.ApplicationGrants;
using IdentityPlatform.Core.Features.Applications;
using IdentityPlatform.Infrastructure.Features.ApplicationGrants;
using Microsoft.AspNetCore.Http.Features;

namespace IdentityPlatform.Admin.Api.Features.ApplicationGrants;

/// <summary>Publishes exact finite grant-job routes under the existing independent bearer and original HTTP lifetime boundary.</summary>
internal static class GrantEndpoint
{
    private const string GuidFormat = "D";

    private const string Separator = "/";
    private const string Current = ".";
    private const string Parent = "..";
    private const string TransferEncoding = "Transfer-Encoding";
    private const string ContentEncoding = "Content-Encoding";

    /// <summary>Requires exact framing, current strong preconditions and one scoped key, then publishes only after durable acceptance and cleanup.</summary>
    internal static Task WriteAsync(HttpContext http, string id, string? jobId = null) => ApplicationHttpBoundary.RunAsync(http, async (budget, clock) =>
    {
        var path = Path(id, jobId) + (jobId is null ? string.Empty : GrantRevocationRules.ContinueSuffix);
        var intent = await InputAsync(http, id, jobId, path);
        var identity = await ApplicationHttpBoundary.AuthenticateAsync(http, budget, clock);
        var result = await GrantAdmissionOperation.ExecuteAsync(http.RequestServices.GetRequiredService<IServiceScopeFactory>(), identity, intent,
            http.RequestServices.GetRequiredService<ApiResourceProfile>(), clock, budget, Guid.NewGuid());
        http.Response.Headers.Location = Path(id, result.Id);
        await ApplicationHttpBoundary.PublishAsync(http, new GrantAcceptance(result, intent.RequestKey.ToString(GuidFormat)), StatusCodes.Status202Accepted,
            result.Version, budget, () => identity.ExpiresAt > clock.GetUtcNow() && result.ExpiresAt > clock.GetUtcNow());
    });

    /// <summary>Reads an exact bodyless recent collection or job under current account and both read permissions.</summary>
    internal static Task ReadAsync(HttpContext http, string id, string? jobId = null) => ApplicationHttpBoundary.RunAsync(http, async (budget, clock) =>
    {
        if (!ApplicationHttpBoundary.Exact(http, Path(id, jobId)) || !HttpMethods.IsGet(http.Request.Method) ||
            http.Request.ContentLength is not (null or 0) || http.Request.Headers.ContainsKey(TransferEncoding) || http.Request.Headers.ContainsKey(ContentEncoding) ||
            http.Features.Get<IHttpRequestBodyDetectionFeature>()?.CanHaveBody == true || await http.Request.Body.ReadAsync(new byte[1], budget.Token) != 0)
        {
            throw new ApplicationHttpRejected(StatusCodes.Status400BadRequest);
        }
        var identity = await ApplicationHttpBoundary.AuthenticateAsync(http, budget, clock);
        var page = await GrantReadOperation.ReadAsync(http.RequestServices.GetRequiredService<IServiceScopeFactory>(), identity, id,
            jobId is null ? null : Guid.Parse(jobId), http.RequestServices.GetRequiredService<ApiResourceProfile>(), clock, budget.Token);
        object result = jobId is null ? page : page.Items.Single();
        await ApplicationHttpBoundary.PublishAsync(http, result, StatusCodes.Status200OK, jobId is null ? null : page.Items.Single().Version, budget,
            () => identity.ExpiresAt > clock.GetUtcNow() && page.ExpiresAt > clock.GetUtcNow());
    });

    /// <summary>Constructs only code-owned routes and rejects dot segments or noncanonical job identifiers independently of routing.</summary>
    private static string Path(string id, string? jobId)
    {
        if (!ApplicationEditorRules.IsText(id, ApplicationProjection.IdentifierLength) || id is Current or Parent || jobId is not null && !GrantRevocationRules.TryKey(jobId, out _))
        {
            throw new ApplicationHttpRejected(StatusCodes.Status400BadRequest);
        }
        return ApplicationListEndpoint.Path + Separator + Uri.EscapeDataString(id) + GrantRevocationRules.RouteSuffix + (jobId is null ? string.Empty : Separator + jobId);
    }

    /// <summary>Rejects duplicate keys, ambiguous JSON and mixed continuation fields before current authentication or database work.</summary>
    private static async Task<GrantIntent> InputAsync(HttpContext http, string id, string? jobId, string path)
    {
        if (http.Request.Headers[GrantRevocationRules.KeyHeader] is not { Count: 1 } keys || !GrantRevocationRules.TryKey(keys[0], out var key))
        {
            throw new ApplicationHttpRejected(StatusCodes.Status400BadRequest);
        }
        try
        {
            var (bytes, version) = await ApplicationMutationInput.ReadAsync(http, path, HttpMethods.Post, precondition: true);
            return GrantIntent.Create(id, version!, key.ToString(GuidFormat), ApplicationJson.Parse<GrantRevocation>(bytes), jobId is null ? null : Guid.Parse(jobId))
                ?? throw new InvalidDataException();
        }
        catch (Exception error) when (error is JsonException or InvalidDataException or ArgumentException)
        {
            throw new ApplicationHttpRejected(StatusCodes.Status400BadRequest);
        }
    }
}
