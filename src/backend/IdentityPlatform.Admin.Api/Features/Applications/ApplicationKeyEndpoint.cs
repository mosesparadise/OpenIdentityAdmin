using System.Text.Json;
using IdentityPlatform.Admin.Api.Features.ApiContracts;
using IdentityPlatform.Admin.Api.Features.Authentication;
using IdentityPlatform.Core.Features.Applications;
using IdentityPlatform.Infrastructure.Features.Applications;

namespace IdentityPlatform.Admin.Api.Features.Applications;

/// <summary>Publishes only locally supplied public key registrations and their current-version replacements under real administrator bearer authority.</summary>
internal static class ApplicationKeyEndpoint
{
    internal const string Machines = MachineClientRules.KeyRegistrationSuffix;
    internal const string Replacements = MachineClientRules.KeyReplacementSuffix;
    internal const string RegisterMachine = "RegisterKeyedMachineApplication";
    internal const string Replace = "ReplaceApplicationPublicKey";
    private const string Collection = "/applications";
    private const string Details = Collection + "/{id}";
    private const string RegistrationSummary = "Register a machine application with a locally supplied public RSA key";
    private const string ReplacementSummary = "Replace an application's public RSA key using its current ETag";
    private const string JsonType = "application/json";

    /// <summary>Maps the versioned routes and their distinct bounded body and response contracts.</summary>
    internal static void MapApplicationKeys(this IEndpointRouteBuilder app)
    {
        var group = app.MapManagementGroup();
        Describe(group.MapPost(Collection + Machines, (Func<HttpContext, Task>)(http => HandleAsync(http, Machines))), RegisterMachine,
            RegistrationSummary).Accepts<ApplicationKeyMachineCreate>(JsonType)
            .Produces<ApplicationDetails>(StatusCodes.Status201Created);
        Describe(group.MapPost(Details + Replacements, (HttpContext http, string id) => HandleAsync(http, Replacements, id)), Replace,
            ReplacementSummary).Accepts<ApplicationKeyReplacement>(JsonType)
            .Produces<ApplicationDetails>().ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed).ProducesProblem(StatusCodes.Status428PreconditionRequired);
    }

    /// <summary>Describes shared authentication, concurrency and safe failure statuses without widening the operation.</summary>
    private static RouteHandlerBuilder Describe(RouteHandlerBuilder route, string operation, string summary) => route.WithName(operation).WithSummary(summary)
        .ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status401Unauthorized).ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status409Conflict).ProducesProblem(StatusCodes.Status503ServiceUnavailable);

    /// <summary>Uses the original identity, recent MFA and atomic audit, returning only metadata once both credential and authority remain current.</summary>
    private static Task HandleAsync(HttpContext http, string suffix, string? id = null) => ApplicationHttpBoundary.RunAsync(http, async (budget, clock) =>
    {
        var command = await ReadAsync(http, suffix, id, clock);
        var identity = await ApplicationHttpBoundary.AuthenticateAsync(http, budget, clock);
        var result = await ApplicationMutationOperation.ExecuteAsync(http.RequestServices.GetRequiredService<IServiceScopeFactory>(), identity, command,
            http.RequestServices.GetRequiredService<ApiResourceProfile>(), clock, budget, Guid.NewGuid());
        if (result.Status != (id is null ? StatusCodes.Status201Created : StatusCodes.Status200OK))
        {
            throw new ApplicationHttpRejected(result.Status);
        }
        if (result.Application is not { Credential: { } credential } details || result.Credential is not null)
        {
            throw new InvalidDataException();
        }
        await ApplicationHttpBoundary.PublishAsync(http, details, result.Status, result.Version, budget,
            () => identity.ExpiresAt > clock.GetUtcNow() && details.ExpiresAt > clock.GetUtcNow() && credential.ExpiresAt > clock.GetUtcNow());
    });

    /// <summary>Rejects unsupported JWK members and noncanonical bodies before passing the immutable public key to the storage operation.</summary>
    private static async Task<ApplicationMutationCommand> ReadAsync(HttpContext http, string suffix, string? id, TimeProvider clock)
    {
        const string Slash = "/";
        try
        {
            var path = ApplicationListEndpoint.Path + (id is null ? string.Empty : Slash + Uri.EscapeDataString(id)) + suffix;
            var (bytes, version) = await ApplicationMutationInput.ReadAsync(http, path, HttpMethods.Post, precondition: id is not null);
            var now = clock.GetUtcNow();
            ApplicationMutationCommand? command = suffix switch
            {
                Machines when ApplicationJson.Parse<ApplicationKeyMachineCreate>(bytes) is { Application: { } machine, PublicKey: { } key } =>
                    ApplicationMutationCommand.CreateWithKey(machine, JsonSerializer.Serialize(key, ApplicationJson.Options), now),
                Replacements when ApplicationJson.Parse<ApplicationKeyReplacement>(bytes) is { PublicKey: { } key } input =>
                    ApplicationMutationCommand.ReplaceKey(id!, version!, JsonSerializer.Serialize(key, ApplicationJson.Options), input.CredentialExpiresAt, input.Confirm, now),
                _ => null
            };
            return command ?? throw new InvalidDataException();
        }
        catch (Exception error) when (error is JsonException or InvalidDataException or ArgumentException)
        {
            throw new ApplicationHttpRejected(StatusCodes.Status400BadRequest);
        }
    }
}
