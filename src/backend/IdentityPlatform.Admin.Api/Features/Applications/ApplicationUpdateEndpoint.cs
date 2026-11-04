using System.Text.Json;
using IdentityPlatform.Admin.Api.Features.Authentication;
using IdentityPlatform.Core.Features.Applications;
using IdentityPlatform.Infrastructure.Features.Applications;

namespace IdentityPlatform.Admin.Api.Features.Applications;

/// <summary>Publishes independently authorized edits and explicit one-time credential replacements through the existing owned aggregate writer.</summary>
internal static class ApplicationUpdateEndpoint
{
    private const string Current = ".";
    private const string Parent = "..";
    private const string Separator = "/";
    private const string Replacement = "/secret-replacements";

    /// <summary>Retains the original deadline through current native authority, atomic persistence, disposal and finite output publication.</summary>
    internal static Task HandleAsync(HttpContext http, string id, bool replacement) => ApplicationHttpBoundary.RunAsync(http, async (budget, clock) =>
    {
        var command = await ReadAsync(http, id, replacement, clock);
        var identity = await ApplicationHttpBoundary.AuthenticateAsync(http, budget, clock);
        var result = await ApplicationMutationOperation.ExecuteAsync(http.RequestServices.GetRequiredService<IServiceScopeFactory>(), identity, command,
            http.RequestServices.GetRequiredService<ApiResourceProfile>(), clock, budget, Guid.NewGuid());
        if (result.Status != StatusCodes.Status200OK || result.Application is not { } application ||
            replacement && result.Credential is null)
        {
            throw new ApplicationHttpRejected(result.Status);
        }
        object output = replacement ? result.Credential! : application;
        await ApplicationHttpBoundary.PublishAsync(http, output, result.Status, result.Version, budget, () =>
            identity.ExpiresAt > clock.GetUtcNow() && application.ExpiresAt > clock.GetUtcNow() &&
            (!replacement || application.Credential!.ExpiresAt > clock.GetUtcNow()));
    });

    /// <summary>Separates framing/precondition errors from malformed immutable-field/confirmation input before native authentication or storage.</summary>
    private static async Task<ApplicationMutationCommand> ReadAsync(HttpContext http, string id, bool replacement, TimeProvider clock)
    {
        if (!ApplicationEditorRules.IsText(id, ApplicationProjection.IdentifierLength) || id is Current or Parent)
        {
            throw new ApplicationHttpRejected(StatusCodes.Status400BadRequest);
        }
        var path = ApplicationListEndpoint.Path + Separator + Uri.EscapeDataString(id) + (replacement ? Replacement : string.Empty);
        try
        {
            var (bytes, version) = await ApplicationMutationInput.ReadAsync(http, path, replacement ? HttpMethods.Post : HttpMethods.Put, precondition: true);
            return (replacement
                ? ApplicationMutationCommand.Replace(id, version!, ApplicationJson.Parse<ApplicationSecretReplacement>(bytes), clock.GetUtcNow())
                : ApplicationMutationCommand.Edit(id, version!, ApplicationJson.Parse<ApplicationEdit>(bytes))) ?? throw new InvalidDataException();
        }
        catch (Exception error) when (error is JsonException or InvalidDataException or ArgumentException)
        {
            throw new ApplicationHttpRejected(StatusCodes.Status400BadRequest);
        }
    }
}
