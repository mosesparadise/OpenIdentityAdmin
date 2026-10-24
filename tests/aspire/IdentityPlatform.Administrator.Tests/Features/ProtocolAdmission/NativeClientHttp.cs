using System.Text.Encodings.Web;
using IdentityPlatform.Admin.Bff.Features.Authentication.Credentials;
using IdentityPlatform.Admin.Bff.Features.Authentication.Protocol;
using IdentityPlatform.Administrator.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using OpenIddict.Client;

namespace IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;

/// <summary>Owns a test-only native ASP.NET challenge/callback host with separate Sessions services and no B1 cookie issuer.</summary>
internal sealed class NativeClientHttp : IAsyncDisposable
{
    internal ServiceProvider Services
    {
        get;
    }
    internal NativeClientIssuerBridge Bridge
    {
        get;
    }
    private readonly NativeClientProfile profile;

    /// <summary>Creates one independent native client instance; supplied protected credentials remain caller-owned until disposal completes.</summary>
    internal NativeClientHttp(BootstrapSessions database, AdministratorProtocolScenario issuer, NativeClientProfile profile, NativeClientCredentials credentials,
        Action<IServiceCollection>? configure = null, bool network = false, IServiceProvider? protocolServices = null)
    {
        this.profile = profile;
        Bridge = new(issuer, protocolServices);
        var services = new ServiceCollection().AddLogging(logging => logging.ClearProviders());
        database.AddPersistence(services);
        services.AddSingleton(UrlEncoder.Default).AddSingleton<TimeProvider>(issuer.Account.Journey.Clock);
        services.AddAdministratorClient(profile, credentials);
        services.AddOpenIddict().AddClient().UseAspNetCore().EnableRedirectionEndpointPassthrough();
        services.AddOpenIddict().AddClient().AddEventHandler<OpenIddictClientEvents.ProcessErrorContext>(builder =>
            builder.SetOrder(int.MinValue).UseInlineHandler(context =>
            {
                context.Transaction.SetProperty(NativeClientRejection.Property, NativeClientRejection.Describe(context.Error, context.ErrorUri));
                return ValueTask.CompletedTask;
            }));
        services.AddOptions<OpenIddictClientOptions>().Configure(options => options.TimeProvider = issuer.Account.Journey.Clock);
        if (!network)
        {
            services.ConfigureAll<HttpClientFactoryOptions>(options =>
        {
            options.HandlerLifetime = Timeout.InfiniteTimeSpan;
            options.HttpMessageHandlerBuilderActions.Add(builder => builder.AdditionalHandlers.Add(Bridge));
        });
        }
        configure?.Invoke(services);
        Services = services.BuildServiceProvider(validateScopes: true);
    }

    /// <summary>Uses the shared assertion-free executor so process-restart evidence exercises the same native HTTP boundary.</summary>
    internal Task<NativeClientHttpResult> InvokeAsync(Uri? callback = null, string? cookie = null, CancellationToken? cancellation = null) =>
        NativeClientRequest.InvokeAsync(Services, profile.ClientUri, callback, cookie, cancellation ?? TestContext.Current.CancellationToken);

    /// <summary>Disposes native singleton and scoped dependencies before the caller releases certificates or drops Sessions.</summary>
    public async ValueTask DisposeAsync()
    {
        try
        {
            await Services.DisposeAsync();
        }
        finally { Bridge.Dispose(); }
    }
}
