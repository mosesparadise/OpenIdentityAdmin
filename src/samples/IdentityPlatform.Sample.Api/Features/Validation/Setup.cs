using Microsoft.IdentityModel.Tokens;
using OpenIddict.Validation;

namespace IdentityPlatform.Sample.Api.Features.Validation;

/// <summary>Configures native public-key resource validation without platform stores or issuer private material.</summary>
internal static class Setup
{
    private const int HandlerOrderOffset = 500;
    private const int NativeResponseLimit = 65536;

    /// <summary>Requires fixed issuer/audience/RS256 and zero skew; no introspection or live revocation claim is introduced.</summary>
    internal static void AddSampleValidation(this IServiceCollection services)
    {
        services.AddAuthentication();
        services.AddOpenIddict().AddValidation(options =>
        {
            options.SetIssuer(SampleProfile.Issuer);
            options.AddAudiences(SampleProfile.Audience);
            options.Configure(value =>
            {
                value.TokenValidationParameters.ClockSkew = TimeSpan.Zero;
                value.TokenValidationParameters.ValidateLifetime = true;
                value.TokenValidationParameters.RequireExpirationTime = true;
                value.TokenValidationParameters.ValidIssuers = [SampleProfile.Issuer];
                value.TokenValidationParameters.ValidAlgorithms = [SecurityAlgorithms.RsaSha256];
            });
            options.AddEventHandler<OpenIddictValidationEvents.HandleConfigurationResponseContext>(handler =>
                handler.UseSingletonHandler<ApiMetadata>().SetOrder(int.MaxValue - HandlerOrderOffset));
            SampleTransport.Add(options);
            options.UseSystemNetHttp(http =>
            {
                http.Configure(value => { value.HttpErrorPolicy = null; value.HttpResiliencePipeline = null; });
                http.ConfigureHttpClient(value => { value.Timeout = SampleProfile.OperationLifetime; value.MaxResponseContentBufferSize = NativeResponseLimit; });
                http.ConfigureHttpClientHandler(value => { value.AllowAutoRedirect = false; value.UseCookies = false; value.AutomaticDecompression = System.Net.DecompressionMethods.None; });
            });
            options.UseAspNetCore().DisableAccessTokenExtractionFromBodyForm().DisableAccessTokenExtractionFromQueryString();
        });
    }
}

/// <summary>Prevents discovery from redirecting public-key retrieval away from the configured issuer.</summary>
internal sealed class ApiMetadata : IOpenIddictValidationHandler<OpenIddictValidationEvents.HandleConfigurationResponseContext>
{
    private const string KeyEndpoint = "connect/jwks";

    /// <summary>Requires exact issuer and JWKS URI before native asymmetric key validation can proceed.</summary>
    public ValueTask HandleAsync(OpenIddictValidationEvents.HandleConfigurationResponseContext context)
    {
        if (context.Configuration.Issuer?.OriginalString != SampleProfile.Issuer ||
            context.Configuration.JsonWebKeySetUri?.OriginalString != SampleProfile.Issuer + KeyEndpoint)
        {
            throw new InvalidOperationException(SampleProfile.Failure);
        }
        return ValueTask.CompletedTask;
    }
}
