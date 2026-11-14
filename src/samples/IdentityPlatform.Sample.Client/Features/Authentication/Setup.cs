using IdentityPlatform.Sample.Client.Features.Configuration;
using IdentityPlatform.Sample.Client.Features.Sessions;
using IdentityPlatform.Sample.Client.Features.State;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenIddict.Abstractions;
using OpenIddict.Client;

namespace IdentityPlatform.Sample.Client.Features.Authentication;

/// <summary>Composes the separate disposable managed client without touching administrator authentication or stores.</summary>
internal static class Setup
{
    private const string AntiforgeryCookie = "__Host-sample-antiforgery";
    private const string SessionCookie = "__Host-sample-session";
    private const int HandlerOrderOffset = 500;
    private const int NativeResponseLimit = 65536;
    private const string CorrelationCookie = "__Host-sample-correlation";

    /// <summary>Registers ephemeral keys, native reference state and opaque cookie tickets; configuration is read lazily after admission.</summary>
    internal static void AddSampleAuthentication(this IServiceCollection services)
    {
        services.TryAddSingleton<TimeProvider>(TimeProvider.System);
        services.TryAddSingleton<SampleConfiguration>();
        services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
        services.AddSingleton<StateLedger>();
        services.AddSingleton<SampleTickets>();
        services.AddSingleton<BrowserAttempts>();
        services.AddSingleton<AttemptCookie>();
        services.AddAntiforgery(options =>
        {
            options.Cookie.Name = AntiforgeryCookie;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Strict;
        });
        services.AddAuthentication(SampleTickets.Scheme).AddCookie(SampleTickets.Scheme, ConfigureCookie);
        services.AddOptions<CookieAuthenticationOptions>(SampleTickets.Scheme).Configure<SampleTickets>((options, tickets) => options.SessionStore = tickets);
        services.AddOpenIddict().AddCore(core => core.SetDefaultTokenEntity<StateRecord>()
            .ReplaceTokenStore<StateRecord, NativeStateStore>().DisableEntityCaching());
        services.AddOpenIddict().AddClient(ConfigureNative);
        services.AddOptions<OpenIddictClientOptions>().Configure<SampleConfiguration>((options, source) =>
            options.Registrations.Add(Registration(source.Read() ?? throw new InvalidOperationException(SampleProfile.Failure))));
    }

    /// <summary>Uses a protected host-only cookie containing only the native server-ticket handle and no sliding lifetime.</summary>
    private static void ConfigureCookie(CookieAuthenticationOptions options)
    {
        options.Cookie.Name = SessionCookie;
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.SlidingExpiration = false;
        options.ExpireTimeSpan = StateLedger.Lifetime;
        options.Events.OnRedirectToLogin = context => { context.Response.StatusCode = StatusCodes.Status401Unauthorized; return Task.CompletedTask; };
        options.Events.OnRedirectToAccessDenied = context => { context.Response.StatusCode = StatusCodes.Status403Forbidden; return Task.CompletedTask; };
    }

    /// <summary>Leaves native crypto/consumption in control while restricting this sample to the supported managed contract.</summary>
    private static void ConfigureNative(OpenIddictClientBuilder client)
    {
        client.AllowAuthorizationCodeFlow().SetRedirectionEndpointUris(SampleProfile.Callback)
            .AddEphemeralEncryptionKey().AddEphemeralSigningKey();
        client.Configure(options =>
        {
            options.ClientUri = new Uri(SampleProfile.Client);
            options.StateTokenLifetime = StateLedger.Lifetime;
        });
        client.AddEventHandler<OpenIddictClientEvents.HandleConfigurationResponseContext>(handler =>
            handler.UseSingletonHandler<SampleMetadata>().SetOrder(int.MaxValue - HandlerOrderOffset));
        client.AddEventHandler<OpenIddictClientEvents.ProcessAuthenticationContext>(handler =>
            handler.UseInlineHandler(context => { context.DisableUserInfoRetrieval = true; return default; })
                .SetOrder(OpenIddictClientHandlers.EvaluateUserInfoRequest.Descriptor.Order - HandlerOrderOffset));
        SampleTransport.Add(client);
        client.UseSystemNetHttp(http =>
        {
            http.Configure(options => { options.HttpErrorPolicy = null; options.HttpResiliencePipeline = null; });
            http.ConfigureHttpClient(value => { value.Timeout = SampleProfile.OperationLifetime; value.MaxResponseContentBufferSize = NativeResponseLimit; });
            http.ConfigureHttpClientHandler(value => { value.AllowAutoRedirect = false; value.UseCookies = false; value.AutomaticDecompression = System.Net.DecompressionMethods.None; });
        });
        client.UseAspNetCore().DisableAutomaticAuthenticationSchemeForwarding().EnableRedirectionEndpointPassthrough().EnableErrorPassthrough().Configure(options =>
        {
            options.CookieBuilder.Name = CorrelationCookie;
            options.CookieBuilder.SecurePolicy = CookieSecurePolicy.Always;
            options.CookieBuilder.SameSite = SameSiteMode.Lax;
            options.CookieBuilder.HttpOnly = true;
        });
    }

    /// <summary>Creates one immutable native registration from the protected manual setup and fixed sample destinations.</summary>
    private static OpenIddictClientRegistration Registration(SampleSettings settings)
    {
        var registration = new OpenIddictClientRegistration
        {
            RegistrationId = SampleProfile.Registration,
            Issuer = new Uri(SampleProfile.Issuer),
            ClientId = settings.ClientId,
            ClientSecret = settings.ClientSecret,
            RedirectUri = new Uri(SampleProfile.Callback),
            DisablePushedAuthorizationRequests = true
        };
        registration.GrantTypes.Add(OpenIddictConstants.GrantTypes.AuthorizationCode);
        registration.ResponseTypes.Add(OpenIddictConstants.ResponseTypes.Code);
        registration.ResponseModes.Add(OpenIddictConstants.ResponseModes.Query);
        registration.CodeChallengeMethods.Add(OpenIddictConstants.CodeChallengeMethods.Sha256);
        registration.ClientAuthenticationMethods.Add(OpenIddictConstants.ClientAuthenticationMethods.ClientSecretBasic);
        registration.Scopes.UnionWith([OpenIddictConstants.Scopes.OpenId, SampleProfile.ApiScope, SampleProfile.EmailScope]);
        registration.TokenValidationParameters.ValidAlgorithms = [SecurityAlgorithms.RsaSha256];
        registration.TokenValidationParameters.ClockSkew = TimeSpan.Zero;
        return registration;
    }
}
