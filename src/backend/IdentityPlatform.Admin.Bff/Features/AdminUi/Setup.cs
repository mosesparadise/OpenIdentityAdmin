using IdentityPlatform.Admin.Bff.Features.BrowserSessions;

namespace IdentityPlatform.Admin.Bff.Features.AdminUi;

/// <summary>Explicitly composes the local workspace without changing default host routing or authentication.</summary>
internal static class Setup
{
    private const string Invalid = "The administrator workspace requires explicit Development composition.";

    /// <summary>Registers only fixed presentation navigation; this marker never grants session or API authority.</summary>
    internal static IServiceCollection AddAdministratorUi(this IServiceCollection services) => services.AddSingleton<BrowserWorkspaceNavigation>();

    /// <summary>Serves exact immutable workspace resources after native browser route ownership, never as a catch-all.</summary>
    internal static IApplicationBuilder UseAdministratorUi(this IApplicationBuilder app)
    {
        if (!app.ApplicationServices.GetRequiredService<IHostEnvironment>().IsDevelopment())
        {
            throw new InvalidOperationException(Invalid);
        }
        _ = app.ApplicationServices.GetRequiredService<BrowserWorkspaceNavigation>();
        return app.Use((http, next) => AdministratorUiAssets.TryGet(http.Request.Path, out var asset)
            ? AdministratorUiAssets.SendAsync(http, asset) : next(http));
    }
}
