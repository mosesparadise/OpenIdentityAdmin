using System.Text.Json;
using IdentityPlatform.Admin.Api.Features.Authentication;
using IdentityPlatform.Core.Features.ApplicationGrants;
using IdentityPlatform.Core.Features.Applications;
using IdentityPlatform.Infrastructure.Features.Applications;

namespace IdentityPlatform.Admin.Api.Features.Applications;

/// <summary>Admits one exact terminal action and publishes only safe application/job versions after actual transaction cleanup.</summary>
internal static class ApplicationRetirementEndpoint
{
    private const string Separator = "/";
    private const string GuidFormat = "D";
    private const string Current = ".";
    private const string Parent = "..";

    /// <summary>Preserves the original HTTP budget and exposes existing durable job status as the accepted response location.</summary>
    internal static Task HandleAsync(HttpContext http, string id) => ApplicationHttpBoundary.RunAsync(http, async (budget, clock) =>
    {
        var intent = await InputAsync(http, id);
        var identity = await ApplicationHttpBoundary.AuthenticateAsync(http, budget, clock);
        var result = await ApplicationRetirementOperation.ExecuteAsync(http.RequestServices.GetRequiredService<IServiceScopeFactory>(), identity, intent,
            http.RequestServices.GetRequiredService<ApiResourceProfile>(), clock, budget, Guid.NewGuid());
        http.Response.Headers.Location = ApplicationListEndpoint.Path + Separator + Uri.EscapeDataString(id) + GrantRevocationRules.RouteSuffix + Separator + result.Job.Id;
        await ApplicationHttpBoundary.PublishAsync(http, result, StatusCodes.Status202Accepted, result.Job.Version, budget,
            () => identity.ExpiresAt > clock.GetUtcNow() && result.Job.ExpiresAt > clock.GetUtcNow());
    });

    /// <summary>Requires canonical identity/key/precondition and strict two-field confirmation, rejecting grant continuation fields on retirement.</summary>
    private static async Task<ApplicationRetirementIntent> InputAsync(HttpContext http, string id)
    {
        if (!ApplicationEditorRules.IsText(id, ApplicationProjection.IdentifierLength) || id is Current or Parent ||
            http.Request.Headers[GrantRevocationRules.KeyHeader] is not { Count: 1 } keys || !GrantRevocationRules.TryKey(keys[0], out var key))
        {
            throw new ApplicationHttpRejected(StatusCodes.Status400BadRequest);
        }
        try
        {
            var path = ApplicationListEndpoint.Path + Separator + Uri.EscapeDataString(id) + ApplicationRetirementRules.Suffix;
            var (bytes, version) = await ApplicationMutationInput.ReadAsync(http, path, HttpMethods.Post, precondition: true);
            return ApplicationRetirementIntent.Create(id, version!, key.ToString(GuidFormat), ApplicationJson.Parse<ApplicationLifecycle>(bytes))
                ?? throw new InvalidDataException();
        }
        catch (Exception error) when (error is JsonException or InvalidDataException or ArgumentException)
        {
            throw new ApplicationHttpRejected(StatusCodes.Status400BadRequest);
        }
    }
}
