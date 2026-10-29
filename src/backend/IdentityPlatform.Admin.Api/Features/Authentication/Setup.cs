using IdentityPlatform.Core.Features.Applications;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenIddict.Validation;
using Polly;

namespace IdentityPlatform.Admin.Api.Features.Authentication;

/// <summary>Composes native remote issuer validation and current Platform stores without issuer private keys.</summary>
internal static class Setup
{
    private const string Invalid = "Invalid management resource configuration.";

    /// <summary>Uses explicit trusted issuer/callback identities and both native lifecycle checks; ordinary default hosting stays dormant.</summary>
    internal static IServiceCollection AddApplicationReads(this IServiceCollection services, Uri issuer, string callback)
    {
        if (!issuer.IsAbsoluteUri || issuer.Scheme != Uri.UriSchemeHttps || issuer.UserInfo.Length != 0 || issuer.Query.Length != 0 || issuer.Fragment.Length != 0 ||
            !Uri.TryCreate(callback, UriKind.Absolute, out var destination) || destination.Scheme != Uri.UriSchemeHttps ||
            destination.UserInfo.Length != 0 || destination.Fragment.Length != 0 || destination.Query.Length != 0 || destination.AbsoluteUri != callback)
        {
            throw new ArgumentException(Invalid);
        }
        services.AddSingleton(new ApiResourceProfile(callback));
        services.TryAddSingleton<TimeProvider>(TimeProvider.System);
        services.AddScoped<NativeOperation>();
        services.AddProtocolStorageSelection();
        services.AddOpenIddict().AddValidation(options =>
        {
            options.SetIssuer(issuer);
            options.AddAudiences(AdministratorProtocolIdentity.Resource);
            options.EnableTokenEntryValidation().EnableAuthorizationEntryValidation();
            options.UseSystemNetHttp().ConfigureHttpClient(client => client.Timeout = TimeSpan.FromSeconds(ApplicationReadBudget.Seconds))
                .ConfigureHttpClientHandler(handler => { handler.AllowAutoRedirect = false; handler.UseCookies = false; })
                .SetHttpErrorPolicy(Policy.NoOpAsync<HttpResponseMessage>());
            options.UseAspNetCore().DisableAccessTokenExtractionFromBodyForm().DisableAccessTokenExtractionFromQueryString();
            options.Configure(configuration => configuration.TokenValidationParameters.ClockSkew = TimeSpan.Zero);
        });
        services.AddOptions<OpenIddictValidationOptions>().Configure<TimeProvider>((options, clock) => options.TimeProvider = clock);
        return services;
    }
}

/// <summary>Retains the configured canonical BFF callback used to verify the current registered profile.</summary>
internal sealed record ApiResourceProfile(string Callback);
