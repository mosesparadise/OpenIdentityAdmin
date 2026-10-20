using IdentityPlatform.AuthServer.Features.Protocol.Denials;
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
        builder.AddNativeErrors();
        builder.AddNativeIssuanceObservations();
        AddRequestPolicy(builder);
        builder.AddEventHandler<OpenIddictServerEvents.ProcessSignInContext>(handler => handler.UseScopedHandler<OpenAdministratorIssuance>()
            .SetOrder(OpenIddictServerHandlers.RestoreInternalClaims.Descriptor.Order + BetweenNativeHandlers));
        builder.AddEventHandler<OpenIddictServerEvents.ProcessSignInContext>(handler => handler.UseScopedHandler<ReserveAdministratorIssuance>()
            .SetOrder(OpenIddictServerHandlers.EvaluateGeneratedTokens.Descriptor.Order + BetweenNativeHandlers));
        builder.AddEventHandler<OpenIddictServerEvents.ProcessSignInContext>(handler => handler.UseScopedHandler<BoundAdministratorIssuance>()
            .SetOrder(OpenIddictServerHandlers.GenerateAccessToken.Descriptor.Order - BetweenNativeHandlers));
        return builder;
    }

    /// <summary>Applies fixed-profile request policy after native validation for each supported endpoint.</summary>
    private static void AddRequestPolicy(OpenIddictServerBuilder builder)
    {
        builder.AddEventHandler<OpenIddictServerEvents.ValidatePushedAuthorizationRequestContext>(handler => handler
            .UseSingletonHandler<AdministratorRequestPolicy>().SetOrder(AdministratorRequestPolicy.AfterNativeValidation));
        builder.AddEventHandler<OpenIddictServerEvents.ValidateAuthorizationRequestContext>(handler => handler
            .UseSingletonHandler<AdministratorRequestPolicy>().SetOrder(AdministratorRequestPolicy.AfterNativeValidation));
        builder.AddEventHandler<OpenIddictServerEvents.ValidateTokenRequestContext>(handler => handler
            .UseSingletonHandler<AdministratorRequestPolicy>().SetOrder(AdministratorRequestPolicy.AfterNativeValidation));
    }

    /// <summary>Explicitly composes account-bound issuance together with its mandatory early durable native redemption.</summary>
    internal static OpenIddictServerBuilder AddAdministratorAccountIssuance(this OpenIddictServerBuilder builder)
    {
        builder.AddAdministratorParIssuance().AddDurableServerConsumption();
        builder.Services.AddSingleton<AdministratorAccountIssuanceEnabled>();
        AddPassthrough<OpenIddictServerEvents.HandleAuthorizationRequestContext,
            OpenIddictServerAspNetCoreHandlerFilters.RequireAuthorizationEndpointPassthroughEnabled>(builder);
        AddPassthrough<OpenIddictServerEvents.HandleTokenRequestContext,
            OpenIddictServerAspNetCoreHandlerFilters.RequireTokenEndpointPassthroughEnabled>(builder);
        return builder;
    }

    /// <summary>Preserves original native passthrough filters and ordering while recording its genuine endpoint handoff.</summary>
    private static void AddPassthrough<TContext, TFilter>(OpenIddictServerBuilder builder)
        where TContext : OpenIddictServerEvents.BaseRequestContext
        where TFilter : class, IOpenIddictServerHandlerFilter<TContext>
    {
        var descriptor = OpenIddictServerAspNetCoreHandlers.EnablePassthroughMode<TContext, TFilter>.Descriptor;
        builder.RemoveEventHandler(descriptor);
        builder.Services.AddSingleton<OpenIddictServerAspNetCoreHandlers.EnablePassthroughMode<TContext, TFilter>>();
        builder.AddEventHandler(OpenIddictServerHandlerDescriptor.CreateBuilder<TContext>().Import(descriptor)
            .UseSingletonHandler<NativeAdministratorPassthrough<TContext, TFilter>>().SetType(OpenIddictServerHandlerType.Custom).Build());
    }

    /// <summary>Retains actual native handlers and imports their original filters/orders into explicitly selected wrappers.</summary>
    internal static OpenIddictServerBuilder AddNativeIssuanceObservations(this OpenIddictServerBuilder builder)
    {
        AddAuthorization(builder);
        ReplaceGeneration<OpenIddictServerHandlers.Protection.AttachSecurityCredentials, NativeTokenCredentials>(builder,
            OpenIddictServerHandlers.Protection.AttachSecurityCredentials.Descriptor);
        ReplaceGeneration<OpenIddictServerHandlers.Protection.AttachTokenSubject, NativeTokenSubject>(builder,
            OpenIddictServerHandlers.Protection.AttachTokenSubject.Descriptor);
        ReplaceGeneration<OpenIddictServerHandlers.Protection.AttachTokenMetadata, NativeTokenMetadata>(builder,
            OpenIddictServerHandlers.Protection.AttachTokenMetadata.Descriptor);
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

    /// <summary>Observes actual ad-hoc authorization creation while retaining every native filter and ordering constraint.</summary>
    private static void AddAuthorization(OpenIddictServerBuilder builder)
    {
        var descriptor = OpenIddictServerHandlers.AttachAuthorization.Descriptor;
        builder.RemoveEventHandler(descriptor);
        builder.Services.AddScoped<OpenIddictServerHandlers.AttachAuthorization>();
        builder.AddEventHandler(OpenIddictServerHandlerDescriptor.CreateBuilder<OpenIddictServerEvents.ProcessSignInContext>()
            .Import(descriptor).UseScopedHandler<NativeAuthorizationCreation>().SetType(OpenIddictServerHandlerType.Custom).Build());
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
    /// <summary>Preserves native descriptor filters/order and explicit delegated service registration after removing its pipeline slot.</summary>
    private static void ReplaceGeneration<TNative, TObserver>(OpenIddictServerBuilder builder, OpenIddictServerHandlerDescriptor descriptor)
        where TNative : class, IOpenIddictServerHandler<OpenIddictServerEvents.GenerateTokenContext>
        where TObserver : class, IOpenIddictServerHandler<OpenIddictServerEvents.GenerateTokenContext>
    {
        builder.RemoveEventHandler(descriptor);
        builder.Services.AddScoped<TNative>();
        builder.AddEventHandler(OpenIddictServerHandlerDescriptor.CreateBuilder<OpenIddictServerEvents.GenerateTokenContext>()
            .Import(descriptor).UseScopedHandler<TObserver>().SetType(OpenIddictServerHandlerType.Custom).Build());
    }
}

/// <summary>Marks only explicit internal account-issuance composition; it neither maps endpoints nor activates production.</summary>
internal sealed class AdministratorAccountIssuanceEnabled
{
}
