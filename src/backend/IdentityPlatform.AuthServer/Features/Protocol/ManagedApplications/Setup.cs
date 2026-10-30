using IdentityPlatform.AuthServer.Features.Protocol.Hosting;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using OpenIddict.Server;
using OpenIddict.Server.AspNetCore;

namespace IdentityPlatform.AuthServer.Features.Protocol.ManagedApplications;

/// <summary>Composes explicit managed native caching without granting administrator proof or enabling incomplete human/token issuance.</summary>
internal static class Setup
{
    /// <summary>Imports the real native self-redirection descriptor and adds a principal-boundary check before native generators execute.</summary>
    internal static OpenIddictServerBuilder AddManagedApplications(this OpenIddictServerBuilder builder)
    {
        builder.Services.AddScoped<ManagedCacheCoordinator>();
        builder.AddEventHandler<OpenIddictServerEvents.ProcessSignInContext>(handler => handler.UseSingletonHandler<PrepareManagedCache>()
            .SetOrder(OpenIddictServerHandlers.GenerateAccessToken.Descriptor.Order - 1));
        var descriptor = OpenIddictServerAspNetCoreHandlers.Authentication.ProcessSelfRedirection.Descriptor;
        builder.RemoveEventHandler(descriptor);
        builder.Services.AddSingleton<OpenIddictServerAspNetCoreHandlers.Authentication.ProcessSelfRedirection>();
        builder.AddEventHandler(OpenIddictServerHandlerDescriptor.CreateBuilder<OpenIddictServerEvents.ApplyAuthorizationResponseContext>()
            .Import(descriptor).UseSingletonHandler<ManagedSelfRedirection>().SetType(OpenIddictServerHandlerType.Custom).Build());
        return builder;
    }

    /// <summary>Identifies only an explicitly enabled nonfixed candidate; actual native validation and current policy remain mandatory.</summary>
    internal static bool Managed(OpenIddictServerEvents.BaseContext context, IServiceProvider services) =>
        services.GetService<ProtocolProfiles>() is { Managed: true } && ManagedDemand.Candidate(context.Transaction.Request?.ClientId);
}

/// <summary>Bounds prepared cached principals after native preparation and before any actual token creation.</summary>
internal sealed class PrepareManagedCache : IOpenIddictServerHandler<OpenIddictServerEvents.ProcessSignInContext>
{
    /// <summary>Touches only the privately coordinated cache; fixed profile principal preparation remains unchanged.</summary>
    public ValueTask HandleAsync(OpenIddictServerEvents.ProcessSignInContext context)
    {
        ManagedCacheSession.Find(context.Transaction)?.Prepare();
        return ValueTask.CompletedTask;
    }
}

/// <summary>Observes native self-redirection solely for a privately owned managed cache generation.</summary>
internal sealed class ManagedSelfRedirection(OpenIddictServerAspNetCoreHandlers.Authentication.ProcessSelfRedirection native)
    : IOpenIddictServerHandler<OpenIddictServerEvents.ApplyAuthorizationResponseContext>
{
    /// <summary>Delegates pilot and fixed paths unchanged; managed cache output requires actual formatter observation.</summary>
    public ValueTask HandleAsync(OpenIddictServerEvents.ApplyAuthorizationResponseContext context) =>
        ManagedCacheProof.Find(context.Transaction) is { } proof ? proof.FormatAsync(context, () => native.HandleAsync(context)) : native.HandleAsync(context);
}
