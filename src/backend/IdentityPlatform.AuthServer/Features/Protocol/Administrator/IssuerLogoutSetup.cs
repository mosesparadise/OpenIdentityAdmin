using IdentityPlatform.AuthServer.Features.Accounts.Browser;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using OpenIddict.Server;
using OpenIddict.Server.AspNetCore;

namespace IdentityPlatform.AuthServer.Features.Protocol.Administrator;

/// <summary>Explicitly opts internal browser hosts into fixed native end-session without enabling production composition.</summary>
internal static class IssuerLogoutSetup
{
    /// <summary>Installs the native endpoint and exact query-formatter observer; native extraction and validation remain library-owned.</summary>
    internal static IServiceCollection AddIssuerLogout(this IServiceCollection services)
    {
        services.AddSingleton<IssuerLogoutEnabled>().AddScoped<IssuerLogoutConfirmation>();
        var builder = services.AddOpenIddict().AddServer().SetEndSessionEndpointUris(IssuerLogoutInput.Path);
        builder.AddEventHandler<OpenIddictServerEvents.HandleEndSessionRequestContext>(handler => handler
            .UseSingletonHandler<IssuerLogoutHandler>().SetOrder(int.MinValue));
        var descriptor = OpenIddictServerAspNetCoreHandlers.Session.ProcessQueryResponse.Descriptor;
        builder.RemoveEventHandler(descriptor);
        services.AddSingleton<OpenIddictServerAspNetCoreHandlers.Session.ProcessQueryResponse>();
        builder.AddEventHandler(OpenIddictServerHandlerDescriptor.CreateBuilder<OpenIddictServerEvents.ApplyEndSessionResponseContext>()
            .Import(descriptor).UseSingletonHandler<IssuerLogoutFormatter>().SetType(OpenIddictServerHandlerType.Custom).Build());
        return services;
    }
}

/// <summary>Marks only explicit internal native end-session composition.</summary>
internal sealed class IssuerLogoutEnabled;

/// <summary>Handles the real post-validation event without introducing a second application passthrough route.</summary>
internal sealed class IssuerLogoutHandler : IOpenIddictServerHandler<OpenIddictServerEvents.HandleEndSessionRequestContext>
{
    /// <summary>Requires the private actual-validation receipt before displaying or processing confirmation.</summary>
    public ValueTask HandleAsync(OpenIddictServerEvents.HandleEndSessionRequestContext context) =>
        (IssuerLogoutFlow.Find(context.Transaction) ?? throw ProtocolStorageOwner.Unavailable()).RunAsync(context);
}

/// <summary>Observes the pinned native serializer only on privately authorized logout output.</summary>
internal sealed class IssuerLogoutFormatter(OpenIddictServerAspNetCoreHandlers.Session.ProcessQueryResponse native)
    : IOpenIddictServerHandler<OpenIddictServerEvents.ApplyEndSessionResponseContext>
{
    /// <summary>Preserves native error formatting when no success flow was authorized; the outer boundary still denies unverified publication.</summary>
    public ValueTask HandleAsync(OpenIddictServerEvents.ApplyEndSessionResponseContext context) => IssuerLogoutFlow.Find(context.Transaction) is { } flow
        ? flow.FormatAsync(context, () => native.HandleAsync(context)) : native.HandleAsync(context);
}
