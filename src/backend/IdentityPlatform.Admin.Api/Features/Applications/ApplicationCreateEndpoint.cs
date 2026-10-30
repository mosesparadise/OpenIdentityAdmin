using System.Text.Json;
using IdentityPlatform.Admin.Api.Features.Authentication;
using IdentityPlatform.Core.Features.Applications;
using IdentityPlatform.Infrastructure.Features.Applications;
using Microsoft.AspNetCore.Http.Features;

namespace IdentityPlatform.Admin.Api.Features.Applications;

/// <summary>Creates one native managed registration using independent current write authority and returns its secret only after atomic commit and cleanup.</summary>
internal static class ApplicationCreateEndpoint
{
    private const int BodyBytes = 32768;
    private const string JsonType = "application/json";
    private const string JsonUtf8 = "application/json; charset=utf-8";
    private const string TransferEncoding = "Transfer-Encoding";
    private const string ContentEncoding = "Content-Encoding";

    /// <summary>Retains one original deadline and exact native bearer identity through the existing owned mutation coordinator.</summary>
    internal static Task HandleAsync(HttpContext http) => ApplicationHttpBoundary.RunAsync(http, async (budget, clock) =>
    {
        var command = await ReadAsync(http, clock);
        var identity = await ApplicationHttpBoundary.AuthenticateAsync(http, budget, clock);
        var result = await ApplicationMutationOperation.ExecuteAsync(http.RequestServices.GetRequiredService<IServiceScopeFactory>(), identity, command,
            http.RequestServices.GetRequiredService<ApiResourceProfile>(), clock, budget, Guid.NewGuid());
        if (result.Status != StatusCodes.Status201Created || result.Credential is not { } credential)
        {
            throw new ApplicationHttpRejected(result.Status);
        }
        await ApplicationHttpBoundary.PublishAsync(http, credential, result.Status, result.Version, budget, () =>
            identity.ExpiresAt > clock.GetUtcNow() && credential.Application.ExpiresAt > clock.GetUtcNow() &&
            credential.Application.Credential!.ExpiresAt > clock.GetUtcNow());
    });

    /// <summary>Checks exact framing and actual bytes before strict JSON parsing; no precondition or unsupported property is silently accepted.</summary>
    private static async Task<ApplicationMutationCommand> ReadAsync(HttpContext http, TimeProvider clock)
    {
        var request = http.Request;
        if (!ApplicationHttpBoundary.Exact(http, ApplicationListEndpoint.Path) || !HttpMethods.IsPost(request.Method) ||
            request.ContentType is not (JsonType or JsonUtf8) || request.ContentLength is < 0 or > BodyBytes ||
            request.Headers.ContainsKey(TransferEncoding) || request.Headers.ContainsKey(ContentEncoding) || request.Headers.IfMatch.Count != 0 ||
            http.Features.Get<IFormFeature>() is not null)
        {
            throw new ApplicationHttpRejected(StatusCodes.Status400BadRequest);
        }
        try
        {
            var bytes = await ApplicationJson.ReadAsync(request.Body, BodyBytes, http.RequestAborted);
            if (request.ContentLength is { } length && length != bytes.Length)
            {
                throw new InvalidDataException();
            }
            return ApplicationMutationCommand.Create(ApplicationJson.Parse<ApplicationCreate>(bytes), clock.GetUtcNow()) ?? throw new InvalidDataException();
        }
        catch (Exception error) when (error is JsonException or InvalidDataException or ArgumentException)
        {
            throw new ApplicationHttpRejected(StatusCodes.Status400BadRequest);
        }
    }
}
