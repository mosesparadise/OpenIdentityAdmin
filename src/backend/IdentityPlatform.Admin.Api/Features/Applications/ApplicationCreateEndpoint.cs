using System.Text.Json;
using IdentityPlatform.Admin.Api.Features.Authentication;
using IdentityPlatform.Core.Features.Applications;
using IdentityPlatform.Infrastructure.Features.Applications;

namespace IdentityPlatform.Admin.Api.Features.Applications;

/// <summary>Creates one native managed registration using independent current write authority and returns its secret only after atomic commit and cleanup.</summary>
internal static class ApplicationCreateEndpoint
{
    private const string Separator = "/";
    /// <summary>Retains one original deadline and exact native bearer identity through the existing owned mutation coordinator.</summary>
    internal const string MachineOperation = "RegisterMachineApplication";
    internal const string MachineSummary = "Register a confidential machine client for assigned API scopes";

    /// <summary>Registers a service with no interactive callbacks and a generated secret disclosed only once.</summary>
    internal static Task HandleMachineAsync(HttpContext http) => HandleCreationAsync(http, null, machine: true);

    /// <summary>Registers an interactive confidential web client.</summary>
    internal static Task HandleAsync(HttpContext http) => HandleCreationAsync(http, null);

    /// <summary>Creates an independently credentialed clone while retaining the exact source precondition and original authority receipt.</summary>
    internal static Task HandleCloneAsync(HttpContext http, string id) => HandleCreationAsync(http, id);

    /// <summary>Publishes a new native identity and one-time credential only after owned commit, cleanup and original deadline checks.</summary>
    private static Task HandleCreationAsync(HttpContext http, string? sourceId, bool machine = false) => ApplicationHttpBoundary.RunAsync(http, async (budget, clock) =>
    {
        var command = await ReadAsync(http, clock, sourceId, machine);
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
    private static async Task<ApplicationMutationCommand> ReadAsync(HttpContext http, TimeProvider clock, string? sourceId, bool machine)
    {
        try
        {
            var path = machine ? ApplicationListEndpoint.Path + MachineClientRules.Suffix : sourceId is null ? ApplicationListEndpoint.Path : ApplicationListEndpoint.Path + Separator + Uri.EscapeDataString(sourceId) + ApplicationCloneRules.Suffix;
            var (bytes, version) = await ApplicationMutationInput.ReadAsync(http, path, HttpMethods.Post, precondition: sourceId is not null);
            return (machine ? ApplicationMutationCommand.CreateMachine(ApplicationJson.Parse<ApplicationMachineCreate>(bytes), clock.GetUtcNow()) : sourceId is null
                ? ApplicationMutationCommand.Create(ApplicationJson.Parse<ApplicationCreate>(bytes), clock.GetUtcNow())
                : ApplicationMutationCommand.Clone(sourceId, version!, ApplicationJson.Parse<ApplicationClone>(bytes), clock.GetUtcNow())) ?? throw new InvalidDataException();
        }
        catch (Exception error) when (error is JsonException or InvalidDataException or ArgumentException)
        {
            throw new ApplicationHttpRejected(StatusCodes.Status400BadRequest);
        }
    }
}
