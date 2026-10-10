namespace IdentityPlatform.AuthServer.Features.Accounts.Login;

internal static class Setup
{
    internal static IServiceCollection AddLogin(this IServiceCollection services)
    {
        services.AddScoped<LoginInteractions>();
        services.AddSingleton<DummyPassword>();
        return services;
    }
}
