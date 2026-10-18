using IdentityPlatform.AuthServer.Features.Protocol.Ownership;
using OpenIddict.Server;
using OpenIddict.Server.AspNetCore;

namespace IdentityPlatform.AuthServer.Features.Protocol.Administrator;

/// <summary>Offers dormant pinned-native issuance observations without enabling routes or administrator composition.</summary>
internal static class Setup
{
    private const int BetweenNativeHandlers = 500;

    /// <summary>Enables only the dormant PAR coordinator while later account-bound issuance integration remains separately incomplete.</summary>
    internal static OpenIddictServerBuilder AddAdministratorParIssuance(this OpenIddictServerBuilder builder)
    {
        builder.Services.AddOwnedServerDispatcher();
        builder.Services.AddScoped<AdministratorIssuanceCoordinator>();
        builder.AddNativeIssuanceObservations();
        builder.AddEventHandler<OpenIddictServerEvents.ProcessSignInContext>(handler => handler.UseScopedHandler<OpenAdministratorIssuance>()
            .SetOrder(OpenIddictServerHandlers.RestoreInternalClaims.Descriptor.Order + BetweenNativeHandlers));
        builder.AddEventHandler<OpenIddictServerEvents.ProcessSignInContext>(handler => handler.UseScopedHandler<ReserveAdministratorIssuance>()
            .SetOrder(OpenIddictServerHandlers.EvaluateGeneratedTokens.Descriptor.Order + BetweenNativeHandlers));
        builder.AddEventHandler<OpenIddictServerEvents.ProcessSignInContext>(handler => handler.UseScopedHandler<BoundAdministratorIssuance>()
            .SetOrder(OpenIddictServerHandlers.GenerateAccessToken.Descriptor.Order - BetweenNativeHandlers));
        return builder;
    }

    /// <summary>Retains actual native handlers and imports their original filters/orders into explicitly selected wrappers.</summary>
    internal static OpenIddictServerBuilder AddNativeIssuanceObservations(this OpenIddictServerBuilder builder)
    {
        var creation = OpenIddictServerHandlers.Protection.CreateTokenEntry.Descriptor;
        var signing = OpenIddictServerHandlers.Protection.GenerateIdentityModelToken.Descriptor;
        var payload = OpenIddictServerHandlers.Protection.AttachTokenPayload.Descriptor;
        builder.RemoveEventHandler(creation).RemoveEventHandler(signing).RemoveEventHandler(payload);
        builder.Services.AddScoped<OpenIddictServerHandlers.Protection.CreateTokenEntry>();
        builder.Services.AddSingleton<OpenIddictServerHandlers.Protection.GenerateIdentityModelToken>();
        builder.Services.AddScoped<OpenIddictServerHandlers.Protection.AttachTokenPayload>();
        builder.AddEventHandler(OpenIddictServerHandlerDescriptor.CreateBuilder<OpenIddictServerEvents.GenerateTokenContext>()
            .Import(creation).UseScopedHandler<NativeTokenCreation>().SetType(OpenIddictServerHandlerType.Custom).Build());
        builder.AddEventHandler(OpenIddictServerHandlerDescriptor.CreateBuilder<OpenIddictServerEvents.GenerateTokenContext>()
            .Import(signing).UseScopedHandler<NativeTokenSigning>().SetType(OpenIddictServerHandlerType.Custom).Build());
        builder.AddEventHandler(OpenIddictServerHandlerDescriptor.CreateBuilder<OpenIddictServerEvents.GenerateTokenContext>()
            .Import(payload).UseScopedHandler<NativeTokenPayload>().SetType(OpenIddictServerHandlerType.Custom).Build());
        AddJson<OpenIddictServerEvents.ApplyPushedAuthorizationResponseContext>(builder);
        AddJson<OpenIddictServerEvents.ApplyTokenResponseContext>(builder);
        var query = OpenIddictServerAspNetCoreHandlers.Authentication.ProcessQueryResponse.Descriptor;
        builder.RemoveEventHandler(query);
        builder.Services.AddSingleton<OpenIddictServerAspNetCoreHandlers.Authentication.ProcessQueryResponse>();
        builder.AddEventHandler(OpenIddictServerHandlerDescriptor.CreateBuilder<OpenIddictServerEvents.ApplyAuthorizationResponseContext>()
            .Import(query).UseScopedHandler<NativeQueryResponse>().SetType(OpenIddictServerHandlerType.Custom).Build());
        return builder;
    }

    /// <summary>Keeps endpoint-specific native JSON descriptors intact while observing actual serialization and handled completion.</summary>
    private static void AddJson<TContext>(OpenIddictServerBuilder builder) where TContext : OpenIddictServerEvents.BaseRequestContext
    {
        var descriptor = OpenIddictServerAspNetCoreHandlers.ProcessJsonResponse<TContext>.Descriptor;
        builder.RemoveEventHandler(descriptor);
        builder.Services.AddSingleton<OpenIddictServerAspNetCoreHandlers.ProcessJsonResponse<TContext>>();
        builder.AddEventHandler(OpenIddictServerHandlerDescriptor.CreateBuilder<TContext>().Import(descriptor)
            .UseScopedHandler<NativeJsonResponse<TContext>>().SetType(OpenIddictServerHandlerType.Custom).Build());
    }
}
