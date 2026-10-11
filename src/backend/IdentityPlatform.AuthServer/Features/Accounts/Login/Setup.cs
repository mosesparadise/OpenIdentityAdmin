namespace IdentityPlatform.AuthServer.Features.Accounts.Login;

internal static class Setup
{
    internal static IServiceCollection AddLogin(this IServiceCollection services)
    {
        services.AddScoped<LoginInteractions>();
        services.AddScoped<LoginInteractionAdmission>();
        services.AddSingleton<DummyPassword>();
        return services;
    }
}
