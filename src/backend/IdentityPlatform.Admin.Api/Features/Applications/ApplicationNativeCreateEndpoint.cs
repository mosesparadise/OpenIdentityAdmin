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

    internal const string DeviceOperation = "RegisterDeviceApplication";
    internal const string DeviceSummary = "Register a public device client for verification on the issuer";

    /// <summary>Registers a device with the fixed verification grant and issuer-owned user interaction.</summary>
    internal static Task HandleDeviceAsync(HttpContext http) => RegisterAsync(http, ApplicationEditorRules.DeviceProfile, ApplicationEditorRules.DeviceRegistrationSuffix);

    /// <summary>Registers a public browser client under the same management authority as a public installed app.</summary>
    internal static Task HandleBrowserAsync(HttpContext http) => RegisterAsync(http, ApplicationEditorRules.BrowserProfile, ApplicationEditorRules.BrowserRegistrationSuffix);

    /// <summary>Registers an installed app using the native callback contract.</summary>
    internal static Task HandleAsync(HttpContext http) => RegisterAsync(http, ApplicationEditorRules.NativeProfile, ApplicationEditorRules.NativeRegistrationSuffix);

    /// <summary>Publishes bounded metadata and a strong version only after the native registration and audit commit.</summary>
    private static Task RegisterAsync(HttpContext http, string profile, string suffix) => ApplicationHttpBoundary.RunAsync(http, async (budget, clock) =>
    {
        var command = await ReadAsync(http, profile, suffix);
        var identity = await ApplicationHttpBoundary.AuthenticateAsync(http, budget, clock);
        var result = await ApplicationMutationOperation.ExecuteAsync(http.RequestServices.GetRequiredService<IServiceScopeFactory>(), identity, command,
            http.RequestServices.GetRequiredService<ApiResourceProfile>(), clock, budget, Guid.NewGuid());
        if (result.Status != StatusCodes.Status201Created)
        {
            throw new ApplicationHttpRejected(result.Status);
        }
        if (result.Application is not { Credential: null } details || result.Credential is not null ||
            details.Profile != profile)
        {
            throw new InvalidDataException();
        }
        await ApplicationHttpBoundary.PublishAsync(http, details, result.Status, result.Version, budget,
            () => identity.ExpiresAt > clock.GetUtcNow() && details.ExpiresAt > clock.GetUtcNow());
    });

    /// <summary>Rejects unexpected fields, duplicate callback values and noncanonical redirects before registration.</summary>
    private static async Task<ApplicationMutationCommand> ReadAsync(HttpContext http, string profile, string suffix)
    {
        try
        {
            var path = ApplicationListEndpoint.Path + suffix;
            var (bytes, _) = await ApplicationMutationInput.ReadAsync(http, path, HttpMethods.Post, precondition: false);
            return (profile switch
            {
                ApplicationEditorRules.DeviceProfile => ApplicationMutationCommand.CreateDevice(ApplicationJson.Parse<ApplicationDeviceCreate>(bytes)),
                ApplicationEditorRules.BrowserProfile => ApplicationMutationCommand.CreateBrowser(ApplicationJson.Parse<ApplicationBrowserCreate>(bytes)),
                ApplicationEditorRules.NativeProfile => ApplicationMutationCommand.CreateNative(ApplicationJson.Parse<ApplicationNativeCreate>(bytes)),
                _ => null
            }) ?? throw new InvalidDataException();
        }
        catch (Exception error) when (error is JsonException or InvalidDataException or ArgumentException)
        {
            throw new ApplicationHttpRejected(StatusCodes.Status400BadRequest);
        }
    }
}
