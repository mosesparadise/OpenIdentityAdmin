using System.Text.Encodings.Web;
using IdentityPlatform.Admin.Bff.Features.Authentication.Credentials;
using IdentityPlatform.Admin.Bff.Features.Authentication.Protocol;
using IdentityPlatform.Admin.Bff.Features.BrowserSessions;
using IdentityPlatform.Admin.Bff.Features.Sessions;
using IdentityPlatform.NativeClient.Driver.Features.Protocol;
using IdentityPlatform.NativeClient.Driver.Features.Requests;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;

namespace IdentityPlatform.NativeClient.Driver.Features.BrowserSessions;

/// <summary>Reimports real B3 keys and runtime persistence in an exited child process, without claiming socket or TLS browser evidence.</summary>
internal static class BrowserDriverRequest
{
    private const string CredentialSection = "ClientCredentials";
    private const string Ready = "ready";

    /// <summary>Reports actual middleware output only after owned request, root and key disposal; outbound HTTP is forbidden.</summary>
    internal static async Task<NativeClientProcessReply> RunAsync(NativeClientProcessInput input, BrowserProcessRequest browser, CancellationToken token)
    {
        var clock = new NativeClientDriverClock(new DateTimeOffset(browser.UtcTicks, TimeSpan.Zero));
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(input.Credentials).AddInMemoryCollection(browser.Protection).Build();
        using var configurationOwner = configuration as IDisposable;
        using var credentials = NativeClientCredentials.Load(configuration.GetSection(CredentialSection), clock.GetUtcNow());
        using var protection = SessionProtection.Create(configuration);
        var profile = NativeClientProfile.Create(input.Issuer, input.Client, input.Callback, input.KeyId, credentials).ForBrowserSessions();
        var transport = new BrowserDriverTransport();
        var services = new ServiceCollection().AddLogging(logging => logging.ClearProviders());
        services.AddSessionPersistence(DatabaseProviderParser.Parse(input.Provider), input.RuntimeConnection);
        services.AddSingleton(UrlEncoder.Default).AddSingleton<TimeProvider>(clock);
        services.AddAdministratorClient(profile, credentials);
        services.AddBrowserLogin(new(input.Issuer, input.Client), protection);
        services.ConfigureAll<HttpClientFactoryOptions>(options => options.HttpMessageHandlerBuilderActions.Add(builder =>
            builder.AdditionalHandlers.Add(transport.Handler())));
        await using var root = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        var application = new ApplicationBuilder(root);
        application.UseBrowserLogin();
        application.Run(_ => throw new InvalidOperationException());
        await NativeClientDriverProtocol.WriteAsync(new
        {
            phase = Ready
        }, token);
        return await BrowserDriverHttp.RunAsync(application.Build(), root, input, browser, transport, token);
    }
}
