using IdentityPlatform.Infrastructure.Features.ProtocolStorage;

namespace IdentityPlatform.Worker.Features.ApplicationGrants;

/// <summary>Composes durable grant processing with owned native stores and no issuer factor keys or browser credentials.</summary>
internal static class Setup
{
    /// <summary>Preserves provider selection and enables exactly the feature's bounded hosted processor.</summary>
    internal static WebApplicationBuilder AddGrantProcessing(this WebApplicationBuilder builder)
    {
        builder.Services.AddProtocolStorageOwnership();
        builder.Services.AddScoped<ProtocolStorageSelection>();
        builder.Services.AddHostedService<GrantWorker>();
        return builder;
    }
}
