namespace IdentityPlatform.Sample;

/// <summary>Defines the single local sample integration; operator input cannot change trust destinations or grants.</summary>
internal static class SampleProfile
{
    private const string EnabledSetting = "LocalDemo:Enabled";
    private const string TrueValue = "true";

    internal const string Issuer = "https://issuer.dev.localhost:17443/";
    internal const string Client = "https://sample.dev.localhost:17446/";
    internal const string Api = "https://orders.dev.localhost:17447/";
    internal const string Callback = Client + "callback";
    internal const string Audience = "orders-api";
    internal const string ApiScope = "orders.read";
    internal const string EmailScope = "sample.email";
    internal const string ConfigurationPath = "/run/identity-demo/sample.json";
    internal const string Registration = "sample-client";
    internal const string Failure = "The disposable sample is unavailable. Check setup or start a fresh local demo.";
    internal const int ResponseLimit = 4096;
    internal static readonly TimeSpan OperationLifetime = TimeSpan.FromSeconds(10);

    /// <summary>Rejects dormant or non-development activation before private sample state is created.</summary>
    internal static void RequireDevelopment(IConfiguration configuration, IHostEnvironment environment)
    {
        if (!environment.IsDevelopment() || configuration[EnabledSetting] != TrueValue)
        {
            throw new InvalidOperationException(Failure);
        }
    }
}
