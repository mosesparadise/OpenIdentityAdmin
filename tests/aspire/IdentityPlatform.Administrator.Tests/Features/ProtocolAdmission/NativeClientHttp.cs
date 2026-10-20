using System.Text.Encodings.Web;
using IdentityPlatform.Admin.Bff.Features.Authentication.Credentials;
using IdentityPlatform.Admin.Bff.Features.Authentication.Protocol;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolTransport;
using IdentityPlatform.Persistence.Tests.Features.ProtocolStorage;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using OpenIddict.Client;
using OpenIddict.Client.AspNetCore;

namespace IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;

/// <summary>Owns a test-only native ASP.NET challenge/callback host with separate Sessions services and no B1 cookie issuer.</summary>
internal sealed class NativeClientHttp : IAsyncDisposable
{
    private const string ChallengePath = "/start";
    private const string CookieSeparator = "; ";
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
        Action<IServiceCollection>? configure = null, bool network = false)
    {
        this.profile = profile;
        Bridge = new(issuer);
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

    /// <summary>Runs a real named native handler under the production response/storage boundary and completes the original host callbacks.</summary>
    internal async Task<NativeClientHttpResult> InvokeAsync(Uri? callback = null, string? cookie = null, CancellationToken? cancellation = null)
    {
        await using var initial = Services.CreateAsyncScope();
        var uri = callback ?? new Uri(profile.ClientUri, ChallengePath);
        var lifetime = new NativeRequestLifetime(cancellation ?? TestContext.Current.CancellationToken);
        var http = new DefaultHttpContext { RequestServices = initial.ServiceProvider };
        http.Features.Set<IHttpRequestLifetimeFeature>(lifetime);
        http.Request.Scheme = uri.Scheme;
        http.Request.Host = new HostString(uri.Host, uri.Port);
        http.Request.Path = uri.AbsolutePath;
        http.Request.QueryString = new QueryString(uri.Query);
        http.Request.Method = HttpMethods.Get;
        if (cookie is not null)
        {
            http.Request.Headers.Cookie = cookie;
        }
        var completion = new NativeHttpCompletion();
        http.Features.Set<IHttpResponseFeature>(completion);
        await using var output = new MemoryStream();
        http.Response.Body = output;
        var result = new NativeClientHttpResult();
        try
        {
            await ProtocolHttpBoundary.RunAsync(http, Services.GetRequiredService<IServiceScopeFactory>(),
                Services.GetRequiredService<TimeProvider>(), async (context, _, _) =>
                {
                    try
                    {
                        await DispatchAsync(context, callback is not null, result);
                    }
                    catch (Exception error) { result.Failure = error; throw; }
                    return ProtocolResponseOutcome.Committed;
                });
        }
        finally { await completion.CompleteAsync(); }
        result.Status = http.Response.StatusCode;
        result.Location = http.Response.Headers.Location.ToString();
        result.Cookies = http.Response.Headers.SetCookie.Where(value => value is not null).Select(value => value!).ToArray();
        result.Cookie = string.Join(CookieSeparator, result.Cookies.Select(value => value.Split(';')[0]));
        result.BodyLength = output.Length;
        result.Aborted = lifetime.Aborted;
        return result;
    }

    /// <summary>Uses native challenge, callback parsing/correlation and authentication results; no principal or context is fabricated.</summary>
    private static async Task DispatchAsync(HttpContext http, bool callback, NativeClientHttpResult result)
    {
        var handler = ActivatorUtilities.CreateInstance<OpenIddictClientAspNetCoreHandler>(http.RequestServices);
        await handler.InitializeAsync(new AuthenticationScheme(OpenIddictClientAspNetCoreDefaults.AuthenticationScheme, null, handler.GetType()), http);
        var handled = await handler.HandleRequestAsync();
        result.Rejection = http.Features.Get<OpenIddictClientAspNetCoreFeature>()?.Transaction?
            .GetProperty<string>(NativeClientRejection.Property) ?? NativeClientRejection.None;
        if (callback)
        {
            if (!handled)
            {
                result.Authentication = await handler.AuthenticateAsync();
            }
        }
        else
        {
            Assert.False(handled);
            await handler.ChallengeAsync(new AuthenticationProperties());
        }
    }

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

/// <summary>Keeps synthetic authentication material in memory without credential-bearing record formatting.</summary>
internal sealed class NativeClientHttpResult
{
    internal bool Aborted
    {
        get; set;
    }
    internal string Rejection { get; set; } = NativeClientRejection.None;
    internal int Status
    {
        get; set;
    }
    internal long BodyLength
    {
        get; set;
    }
    internal string Location { get; set; } = string.Empty;
    internal string Cookie { get; set; } = string.Empty;
    internal string[] Cookies { get; set; } = [];
    internal AuthenticateResult? Authentication
    {
        get; set;
    }
    internal Exception? Failure
    {
        get; set;
    }
}
