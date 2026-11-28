using System.Text.Json;
using IdentityPlatform.Admin.Api.Features.Authentication;
using IdentityPlatform.Core.Features.Applications;
using IdentityPlatform.Infrastructure.Features.Applications;

namespace IdentityPlatform.Admin.Api.Features.Applications;

/// <summary>Creates a public native registration under the existing owned write, audit and bearer authority.</summary>
internal static class ApplicationNativeCreateEndpoint
{
    internal const string Operation = "CreateNativeApplication";
    internal const string Summary = "Register a public native application with exact claimed HTTPS callbacks and S256";

    internal const string BrowserOperation = "RegisterBrowserApplication";
    internal const string BrowserSummary = "Register a public browser client with separate HTTPS origins, exact callbacks and S256";

    /// <summary>Registers a public browser client under the same management authority as a public installed app.</summary>
    internal static Task HandleBrowserAsync(HttpContext http) => RegisterAsync(http, browser: true);

    /// <summary>Registers an installed app using the native callback contract.</summary>
    internal static Task HandleAsync(HttpContext http) => RegisterAsync(http, browser: false);

    /// <summary>Publishes bounded metadata and a strong version only after the native registration and audit commit.</summary>
    private static Task RegisterAsync(HttpContext http, bool browser) => ApplicationHttpBoundary.RunAsync(http, async (budget, clock) =>
    {
        var command = await ReadAsync(http, browser);
        var identity = await ApplicationHttpBoundary.AuthenticateAsync(http, budget, clock);
        var result = await ApplicationMutationOperation.ExecuteAsync(http.RequestServices.GetRequiredService<IServiceScopeFactory>(), identity, command,
            http.RequestServices.GetRequiredService<ApiResourceProfile>(), clock, budget, Guid.NewGuid());
        if (result.Status != StatusCodes.Status201Created)
        {
            throw new ApplicationHttpRejected(result.Status);
        }
        if (result.Application is not { Credential: null } details || result.Credential is not null ||
            details.Profile != (browser ? ApplicationEditorRules.BrowserProfile : ApplicationEditorRules.NativeProfile))
        {
            throw new InvalidDataException();
        }
        await ApplicationHttpBoundary.PublishAsync(http, details, result.Status, result.Version, budget,
            () => identity.ExpiresAt > clock.GetUtcNow() && details.ExpiresAt > clock.GetUtcNow());
    });

    /// <summary>Rejects unexpected fields, duplicate callback values and noncanonical redirects before registration.</summary>
    private static async Task<ApplicationMutationCommand> ReadAsync(HttpContext http, bool browser)
    {
        try
        {
            var path = ApplicationListEndpoint.Path + (browser ? ApplicationEditorRules.BrowserRegistrationSuffix : ApplicationEditorRules.NativeRegistrationSuffix);
            var (bytes, _) = await ApplicationMutationInput.ReadAsync(http, path, HttpMethods.Post, precondition: false);
            return (browser ? ApplicationMutationCommand.CreateBrowser(ApplicationJson.Parse<ApplicationBrowserCreate>(bytes)) :
                ApplicationMutationCommand.CreateNative(ApplicationJson.Parse<ApplicationNativeCreate>(bytes))) ?? throw new InvalidDataException();
        }
        catch (Exception error) when (error is JsonException or InvalidDataException or ArgumentException)
        {
            throw new ApplicationHttpRejected(StatusCodes.Status400BadRequest);
        }
    }
}
