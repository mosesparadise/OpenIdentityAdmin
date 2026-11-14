using IdentityPlatform.AuthServer.Features.Protocol.Denials;
using IdentityPlatform.AuthServer.Features.Protocol.Hosting;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using OpenIddict.Server;
using static OpenIddict.Server.AspNetCore.OpenIddictServerAspNetCoreHandlerFilters;
using static OpenIddict.Server.AspNetCore.OpenIddictServerAspNetCoreHandlers;
using static OpenIddict.Server.OpenIddictServerEvents;

namespace IdentityPlatform.AuthServer.Features.Protocol.UserInfo;

/// <summary>Enables native UserInfo only for the explicitly enabled managed profile, preserving fixed and pilot-only graphs.</summary>
internal static class Setup
{
    /// <summary>Imports native descriptors for extraction, passthrough and bearer-denial formatting without replacing native validation.</summary>
    internal static OpenIddictServerBuilder AddManagedUserInfo(this OpenIddictServerBuilder builder)
    {
        builder.SetUserInfoEndpointUris(ProtocolOptions.UserInfoPath);
        builder.RegisterClaims(OpenIddict.Abstractions.OpenIddictConstants.Claims.Email, OpenIddict.Abstractions.OpenIddictConstants.Claims.EmailVerified);
        builder.UseAspNetCore().EnableUserInfoEndpointPassthrough();
        builder.AddEventHandler<ExtractUserInfoRequestContext>(handler => handler.UseInlineHandler(UserInfoTransport.ValidateAsync)
            .SetOrder(ExtractGetOrPostRequest<ExtractUserInfoRequestContext>.Descriptor.Order - 1));
        var passthrough = EnablePassthroughMode<HandleUserInfoRequestContext, RequireUserInfoEndpointPassthroughEnabled>.Descriptor;
        builder.RemoveEventHandler(passthrough);
        builder.Services.AddSingleton<EnablePassthroughMode<HandleUserInfoRequestContext, RequireUserInfoEndpointPassthroughEnabled>>();
        builder.AddEventHandler(OpenIddictServerHandlerDescriptor.CreateBuilder<HandleUserInfoRequestContext>().Import(passthrough)
            .UseSingletonHandler<NativeUserInfoPassthrough>().SetType(OpenIddictServerHandlerType.Custom).Build());
        AddErrors(builder);
        return builder;
    }

    /// <summary>Adds missing no-store headers and observes the exact native empty-body bearer challenge formatter.</summary>
    private static void AddErrors(OpenIddictServerBuilder builder)
    {
        builder.AddEventHandler<ApplyUserInfoResponseContext>(handler => handler.UseSingletonHandler<NativeErrorFields<ApplyUserInfoResponseContext>>()
            .SetOrder(AttachHttpResponseCode<ApplyUserInfoResponseContext>.Descriptor.Order - 1));
        builder.AddEventHandler(AttachCacheControlHeader<ApplyUserInfoResponseContext>.Descriptor);
        var descriptor = ProcessChallengeErrorResponse<ApplyUserInfoResponseContext>.Descriptor;
        builder.RemoveEventHandler(descriptor);
        builder.Services.AddSingleton<ProcessChallengeErrorResponse<ApplyUserInfoResponseContext>>();
        builder.AddEventHandler(OpenIddictServerHandlerDescriptor.CreateBuilder<ApplyUserInfoResponseContext>().Import(descriptor)
            .UseSingletonHandler<NativeUserInfoError>().SetType(OpenIddictServerHandlerType.Custom).Build());
    }
}

/// <summary>Observes only the real enabled native UserInfo handoff after complete native validation.</summary>
internal sealed class NativeUserInfoPassthrough(EnablePassthroughMode<HandleUserInfoRequestContext, RequireUserInfoEndpointPassthroughEnabled> native)
    : IOpenIddictServerHandler<HandleUserInfoRequestContext>
{
    /// <summary>Rejects a manufactured skipped flag or absent actual native validation receipt.</summary>
    public ValueTask HandleAsync(HandleUserInfoRequestContext context) =>
        (UserInfoAdmission.Find(context.Transaction) ?? throw ProtocolStorageOwner.Unavailable()).ObserveAsync(context, () => native.HandleAsync(context));
}

/// <summary>Retains the native RFC bearer challenge and records exact empty-body error formatting.</summary>
internal sealed class NativeUserInfoError(ProcessChallengeErrorResponse<ApplyUserInfoResponseContext> native) : IOpenIddictServerHandler<ApplyUserInfoResponseContext>
{
    /// <summary>Requires the private finite error receipt for owned native denial publication.</summary>
    public ValueTask HandleAsync(ApplyUserInfoResponseContext context) => NativeErrorFormatter.Find(context.Transaction) is { } proof
        ? proof.ObserveAsync(context, NativeErrorFormat.Bearer, () => native.HandleAsync(context)) : native.HandleAsync(context);
}
