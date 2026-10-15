namespace IdentityPlatform.Admin.Bff.Features.Sessions;

/// <summary>Provides explicit internal opt-in composition; the production host does not call this or map session routes.</summary>
internal static class Setup
{
    /// <summary>Registers the internal store using separately verified caller-owned protection, disposed after these consumers.</summary>
    internal static IServiceCollection AddSessionTickets(this IServiceCollection services, SessionProtection protection, TimeProvider clock)
    {
        services.AddSingleton(new SessionTicketCodec(protection.Provider));
        services.AddSingleton(serviceProvider => new SessionTicketStore(serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            serviceProvider.GetRequiredService<SessionTicketCodec>(), clock));
        return services;
    }
}
