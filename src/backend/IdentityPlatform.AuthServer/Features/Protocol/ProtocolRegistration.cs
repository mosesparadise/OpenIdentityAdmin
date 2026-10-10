using System.Threading.RateLimiting;
using IdentityPlatform.AuthServer.Features.Accounts.Login;
using IdentityPlatform.AuthServer.Features.Issuer.Credentials;
using IdentityPlatform.AuthServer.Features.Pilot;
using IdentityPlatform.AuthServer.Features.Protocol.Policy;
using IdentityPlatform.AuthServer.Features.Protocol.Protection;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using OpenIddict.Abstractions;

namespace IdentityPlatform.AuthServer.Features.Protocol;

public static class ProtocolRegistration
{
    public static void AddFirstPartyPilot(this IServiceCollection services, IssuerCredentials credentials)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<PilotBootstrap>();
        services.AddScoped<PilotEligibility>();
        services.AddScoped<LoginInteractions>();
        services.AddSingleton<DummyPassword>();
        services.AddIdentityCore<PlatformUser>().AddSignInManager();
        services.Configure<IdentityOptions>(options =>
        {
            options.Password.RequiredLength = 16;
            options.Password.RequireDigit = false;
            options.Password.RequireLowercase = false;
            options.Password.RequireUppercase = false;
            options.Password.RequireNonAlphanumeric = false;
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
        });
        services.Configure<PasswordHasherOptions>(options => options.IterationCount = 210_000);
        services.AddAuthorization();
        services.AddAuthentication(IdentityConstants.ApplicationScheme).AddCookie(IdentityConstants.ApplicationScheme, options =>
        {
            options.Cookie.Name = PilotProfile.SessionCookie;
            options.Cookie.Path = "/";
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.ExpireTimeSpan = TimeSpan.FromSeconds(PilotProfile.SessionSeconds);
            options.SlidingExpiration = false;
        });
        services.AddAntiforgery(options =>
        {
            options.Cookie.Name = "__Host-pilot-antiforgery";
            options.Cookie.Path = "/";
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Strict;
        });
        services.Configure<FormOptions>(options =>
        {
            options.ValueCountLimit = 16;
            options.KeyLengthLimit = 128;
            options.ValueLengthLimit = 8192;
            options.MultipartBodyLengthLimit = 16_384;
        });
        services.AddRazorPages(options => options.RootDirectory = "/Features/Accounts");
        services.AddRequestTimeouts(options => options.DefaultPolicy = new() { Timeout = TimeSpan.FromSeconds(10), TimeoutStatusCode = 503 });
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = 429;
            options.OnRejected = async (context, cancellationToken) =>
            {
                context.HttpContext.Response.ContentType = "text/plain; charset=utf-8";
                await context.HttpContext.Response.WriteAsync("Too many sign-in requests. Wait a minute and try again.", cancellationToken);
            };
            options.GlobalLimiter = PartitionedRateLimiter.CreateChained(
                PartitionedRateLimiter.Create<HttpContext, string>(context =>
                context.Request.Path.StartsWithSegments("/connect") || context.Request.Path.StartsWithSegments("/account")
                    ? RateLimitPartition.GetFixedWindowLimiter("pilot", _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 600,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0
                    })
                    : RateLimitPartition.GetNoLimiter("operational")),
                PartitionedRateLimiter.Create<HttpContext, string>(context =>
                    context.Request.Path.StartsWithSegments("/connect") || context.Request.Path.StartsWithSegments("/account")
                        ? RateLimitPartition.GetConcurrencyLimiter("pilot", _ => new ConcurrencyLimiterOptions { PermitLimit = 16, QueueLimit = 0 })
                        : RateLimitPartition.GetNoLimiter("operational")));
        });
        PilotDiagnostics.SuppressProtocolSecrets(services);
        services.AddOpenIddict().AddServer(options =>
        {
            options.SetIssuer(credentials.Issuer);
            options.SetAuthorizationEndpointUris("/connect/authorize");
            options.SetTokenEndpointUris("/connect/token");
            options.SetJsonWebKeySetEndpointUris("/connect/jwks");
            options.AllowAuthorizationCodeFlow();
            options.RequireProofKeyForCodeExchange();
            options.RegisterScopes(OpenIddictConstants.Scopes.OpenId, PilotProfile.Scope);
            options.RegisterClaims(OpenIddictConstants.Claims.AuthenticationTime, OpenIddictConstants.Claims.Nonce, OpenIddictConstants.Claims.AccessTokenHash);
            options.AddSigningCertificate(credentials.Signing);
            options.AddEncryptionCertificate(credentials.Encryption);
            options.DisableAccessTokenEncryption();
            options.SetAuthorizationCodeLifetime(TimeSpan.FromSeconds(60));
            options.SetAccessTokenLifetime(TimeSpan.FromMinutes(5));
            options.SetIdentityTokenLifetime(TimeSpan.FromMinutes(5));
            options.Configure(configuration =>
            {
                configuration.RequestTokenLifetime = TimeSpan.FromSeconds(PilotProfile.InteractionSeconds);
                configuration.CodeChallengeMethods.Clear();
                configuration.CodeChallengeMethods.Add(OpenIddictConstants.CodeChallengeMethods.Sha256);
                configuration.PromptValues.Clear();
                configuration.PromptValues.UnionWith([OpenIddictConstants.PromptValues.None, OpenIddictConstants.PromptValues.Login]);
                configuration.ResponseModes.Clear();
                configuration.ResponseModes.Add(OpenIddictConstants.ResponseModes.Query);
                configuration.ClientAuthenticationMethods.Clear();
                configuration.ClientAuthenticationMethods.Add(OpenIddictConstants.ClientAuthenticationMethods.ClientSecretBasic);
            });
            options.EnableAuthorizationRequestCaching();
            options.UseAspNetCore().EnableAuthorizationEndpointPassthrough().EnableTokenEndpointPassthrough();
        });
    }
}
