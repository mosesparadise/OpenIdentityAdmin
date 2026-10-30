using IdentityPlatform.AuthServer.Features.Protocol.ManagedApplications;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using OpenIddict.Server;
using OpenIddict.Server.AspNetCore;

namespace IdentityPlatform.AuthServer.Features.Protocol.Administrator;

/// <summary>Observes native handoff with separate administrator and restored managed-request proofs, preserving initial native caching.</summary>
internal sealed class NativeAdministratorPassthrough<TContext, TFilter>(OpenIddictServerAspNetCoreHandlers.EnablePassthroughMode<TContext, TFilter> native)
    : IOpenIddictServerHandler<TContext>
    where TContext : OpenIddictServerEvents.BaseRequestContext
    where TFilter : class, IOpenIddictServerHandlerFilter<TContext>
{
    /// <summary>Wraps actual native execution; no endpoint or custom handler can manufacture a receipt by setting skipped state.</summary>
    public ValueTask HandleAsync(TContext context) => context.Transaction.Request?.ClientId == AdministratorProtocolIdentity.ClientId
        ? AdministratorPassthrough.ObserveAsync(context, () => native.HandleAsync(context)) : context is OpenIddictServerEvents.HandleAuthorizationRequestContext authorization && ManagedAdmission.Find(context.Transaction) is { Demand.Cached: true } managed
            ? managed.ObservePassthroughAsync(authorization, () => native.HandleAsync(context)) : native.HandleAsync(context);
}
