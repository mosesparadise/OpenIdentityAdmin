using OpenIddict.Server;
using OpenIddict.Server.AspNetCore;

namespace IdentityPlatform.AuthServer.Features.Protocol.Denials;

/// <summary>Retains pinned native formatter descriptors while observing only privately admitted error continuations.</summary>
internal static class Setup
{
    /// <summary>Checks error fields before header serialization and observes the actual local authorization error formatter.</summary>
    internal static void AddNativeErrors(this OpenIddictServerBuilder builder)
    {
        builder.Services.AddScoped<NativeErrorCoordinator>();
        AddChallengeDefaults(builder);
        AddGuard<OpenIddictServerEvents.ApplyAuthorizationResponseContext>(builder);
        AddGuard<OpenIddictServerEvents.ApplyTokenResponseContext>(builder);
        AddGuard<OpenIddictServerEvents.ApplyPushedAuthorizationResponseContext>(builder);
        var descriptor = OpenIddictServerAspNetCoreHandlers.ProcessLocalErrorResponse<OpenIddictServerEvents.ApplyAuthorizationResponseContext>.Descriptor;
        builder.RemoveEventHandler(descriptor);
        builder.Services.AddSingleton<OpenIddictServerAspNetCoreHandlers.ProcessLocalErrorResponse<OpenIddictServerEvents.ApplyAuthorizationResponseContext>>();
        builder.AddEventHandler(OpenIddictServerHandlerDescriptor.CreateBuilder<OpenIddictServerEvents.ApplyAuthorizationResponseContext>().Import(descriptor)
            .UseScopedHandler<NativeLocalError>().SetType(OpenIddictServerHandlerType.Custom).Build());
    }

    /// <summary>Retains native challenge defaults while normalizing the default URI only for the privately claimed fixed denial.</summary>
    private static void AddChallengeDefaults(OpenIddictServerBuilder builder)
    {
        var descriptor = OpenIddictServerHandlers.AttachDefaultChallengeError.Descriptor;
        builder.RemoveEventHandler(descriptor);
        builder.Services.AddSingleton<OpenIddictServerHandlers.AttachDefaultChallengeError>();
        builder.AddEventHandler(OpenIddictServerHandlerDescriptor.CreateBuilder<OpenIddictServerEvents.ProcessChallengeContext>().Import(descriptor)
            .UseScopedHandler<NativeChallengeDefaults>().SetType(OpenIddictServerHandlerType.Custom).Build());
    }

    /// <summary>Runs after native error/state/issuer normalization and before any HTTP header can serialize the response.</summary>
    private static void AddGuard<TContext>(OpenIddictServerBuilder builder) where TContext : OpenIddictServerEvents.BaseRequestContext =>
        builder.AddEventHandler<TContext>(handler => handler.UseSingletonHandler<NativeErrorFields<TContext>>()
            .SetOrder(OpenIddictServerAspNetCoreHandlers.AttachHttpResponseCode<TContext>.Descriptor.Order - 1));
}

/// <summary>Validates the finite error envelope before native headers and leaves ordinary success/pilot formatting unchanged.</summary>
internal sealed class NativeErrorFields<TContext> : IOpenIddictServerHandler<TContext> where TContext : OpenIddictServerEvents.BaseRequestContext
{
    /// <summary>Requires proof only when the coordinator has installed its private active error continuation.</summary>
    public ValueTask HandleAsync(TContext context)
    {
        NativeErrorFormatter.Find(context.Transaction)?.BeforeHeaders(context);
        return ValueTask.CompletedTask;
    }
}

/// <summary>Observes the actual native local formatter for authorization errors without a validated redirect.</summary>
internal sealed class NativeLocalError(OpenIddictServerAspNetCoreHandlers.ProcessLocalErrorResponse<OpenIddictServerEvents.ApplyAuthorizationResponseContext> native)
    : IOpenIddictServerHandler<OpenIddictServerEvents.ApplyAuthorizationResponseContext>
{
    /// <summary>Requires exact error-only local output while retaining native serialization and handled semantics.</summary>
    public ValueTask HandleAsync(OpenIddictServerEvents.ApplyAuthorizationResponseContext context) => NativeErrorFormatter.Find(context.Transaction) is { } proof
        ? proof.ObserveAsync(context, NativeErrorFormat.Local, () => native.HandleAsync(context)) : native.HandleAsync(context);
}

/// <summary>Preserves actual native challenge normalization while omitting its default documentation URI from a verified finite denial.</summary>
internal sealed class NativeChallengeDefaults(OpenIddictServerHandlers.AttachDefaultChallengeError native)
    : IOpenIddictServerHandler<OpenIddictServerEvents.ProcessChallengeContext>
{
    /// <summary>Normalizes only an active private error proof; ordinary native pilot challenges keep their original handler.</summary>
    public ValueTask HandleAsync(OpenIddictServerEvents.ProcessChallengeContext context) => NativeErrorFormatter.Find(context.Transaction) is { } proof
        ? proof.NormalizeChallengeAsync(context, () => native.HandleAsync(context)) : native.HandleAsync(context);
}
