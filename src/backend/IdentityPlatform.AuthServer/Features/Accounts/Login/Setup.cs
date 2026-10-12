namespace IdentityPlatform.AuthServer.Features.Accounts.Login;

internal static class Setup
{
    /// <summary>Registers pilot interaction handlers while shared account composition owns password work and transport protections.</summary>
    internal static IServiceCollection AddLogin(this IServiceCollection services)
    {
        services.AddScoped<LoginInteractions>();
        services.AddScoped<LoginInteractionAdmission>();
        return services;
    }
}
