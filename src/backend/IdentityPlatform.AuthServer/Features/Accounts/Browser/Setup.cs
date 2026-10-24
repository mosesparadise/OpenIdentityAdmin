namespace IdentityPlatform.AuthServer.Features.Accounts.Browser;

/// <summary>Provides explicit dormant account opt-in without a production activation flag or a second authentication scheme.</summary>
internal static class Setup
{
    /// <summary>Registers only fixed-profile adapters after administrator account composition has supplied actual native protections and journeys.</summary>
    internal static IServiceCollection AddBrowserAccounts(this IServiceCollection services, BrowserAccountProfile profile) => services
        .AddSingleton(profile).AddScoped<BrowserAccountProof>().AddScoped<BrowserAccountJourney>();
}
