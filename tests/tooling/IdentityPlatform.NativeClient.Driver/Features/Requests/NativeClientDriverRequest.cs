using System.Text.Encodings.Web;
using IdentityPlatform.Admin.Bff.Features.Authentication.Credentials;
using IdentityPlatform.Admin.Bff.Features.Authentication.Protocol;
using IdentityPlatform.NativeClient.Driver.Features.BrowserSessions;
using IdentityPlatform.NativeClient.Driver.Features.Protocol;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenIddict.Abstractions;
using OpenIddict.Client;

namespace IdentityPlatform.NativeClient.Driver.Features.Requests;

/// <summary>Reimports durable keys and Sessions services in a genuinely separate process, using normal native HTTPS with no issuer bridge.</summary>
internal static class NativeClientDriverRequest
{
    private const string CredentialSection = "ClientCredentials";
    private const string Ready = "ready";

    /// <summary>Returns a private result only after all request callbacks, native scopes, root services and imported keys finish disposal.</summary>
    internal static async Task<NativeClientProcessReply> RunAsync(NativeClientProcessInput input, CancellationToken token)
    {
        if (input.Browser is { } browser)
        {
            return await BrowserDriverRequest.RunAsync(input, browser, token);
        }
        var clock = new NativeClientDriverClock(DateTimeOffset.FromUnixTimeMilliseconds(input.UtcMilliseconds));
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(input.Credentials).Build();
        using var configurationOwner = configuration as IDisposable;
        using var credentials = NativeClientCredentials.Load(configuration.GetSection(CredentialSection), clock.GetUtcNow());
        var profile = NativeClientProfile.Create(input.Issuer, input.Client, input.Callback, input.KeyId, credentials);
        var services = new ServiceCollection().AddLogging(logging => logging.ClearProviders());
        services.AddSessionPersistence(DatabaseProviderParser.Parse(input.Provider), input.RuntimeConnection);
        services.AddSingleton(UrlEncoder.Default).AddSingleton<TimeProvider>(clock);
        services.AddAdministratorClient(profile, credentials);
        services.AddOpenIddict().AddClient().UseAspNetCore().EnableRedirectionEndpointPassthrough();
        services.AddOptions<OpenIddictClientOptions>().Configure(options => options.TimeProvider = clock);
        services.AddOpenIddict().AddClient().AddEventHandler<OpenIddictClientEvents.ProcessErrorContext>(builder => builder
            .SetOrder(int.MinValue).UseInlineHandler(context =>
            {
                context.Transaction.SetProperty(NativeClientRejection.Property, NativeClientRejection.Describe(context.Error, context.ErrorUri));
                return ValueTask.CompletedTask;
            }));
        await using var root = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        await NativeClientDriverProtocol.WriteAsync(new
        {
            phase = Ready
        }, token);
        var result = await NativeClientRequest.InvokeAsync(root, profile.ClientUri, input.RequestUri is null ? null : new Uri(input.RequestUri), input.Cookie, token);
        if (result.Failure is not null)
        {
            throw new InvalidOperationException();
        }
        return Project(result);
    }

    /// <summary>Projects only actual authentication success/subject and HTTP evidence, excluding tokens, ticket properties and exception text.</summary>
    private static NativeClientProcessReply Project(NativeClientHttpResult result) => new()
    {
        Status = result.Status,
        BodyLength = result.BodyLength,
        Location = result.Location,
        Cookies = result.Cookies,
        Authenticated = result.Authentication?.Succeeded == true,
        Subject = result.Authentication?.Principal?.GetClaim(OpenIddictConstants.Claims.Subject),
        Aborted = result.Aborted,
        Rejection = result.Rejection
    };
}

/// <summary>Shares only the parent's synthetic UTC policy snapshot; inherited monotonic timestamps and timers keep real deadlines unchanged.</summary>
internal sealed class NativeClientDriverClock(DateTimeOffset now) : TimeProvider
{
    /// <summary>Preserves identical native issuance/state policy time across exited client processes and the parent issuer.</summary>
    public override DateTimeOffset GetUtcNow() => now;
}
