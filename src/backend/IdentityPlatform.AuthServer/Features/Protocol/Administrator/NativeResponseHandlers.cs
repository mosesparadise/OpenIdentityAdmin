using IdentityPlatform.AuthServer.Features.Protocol.Denials;
using OpenIddict.Server;
using OpenIddict.Server.AspNetCore;

namespace IdentityPlatform.AuthServer.Features.Protocol.Administrator;

/// <summary>Preserves actual native JSON serialization while recording completed formatter output for owned issuance.</summary>
internal sealed class NativeJsonResponse<TContext>(OpenIddictServerAspNetCoreHandlers.ProcessJsonResponse<TContext> native)
    : IOpenIddictServerHandler<TContext> where TContext : OpenIddictServerEvents.BaseRequestContext
{
    /// <summary>Observes only explicitly active administrator issuance; ordinary native errors and pilot responses remain delegated unchanged.</summary>
    public ValueTask HandleAsync(TContext context) => NativeIssuanceProof.Find(context.Transaction) is { } proof
        ? proof.FormatAsync(context, () => native.HandleAsync(context)) : NativeErrorFormatter.Find(context.Transaction) is { } error
            ? error.ObserveAsync(context, NativeErrorFormat.Json, () => native.HandleAsync(context)) : native.HandleAsync(context);
}

/// <summary>Preserves the native query formatter and its standard response encoding.</summary>
internal sealed class NativeQueryResponse(OpenIddictServerAspNetCoreHandlers.Authentication.ProcessQueryResponse native)
    : IOpenIddictServerHandler<OpenIddictServerEvents.ApplyAuthorizationResponseContext>
{
    /// <summary>Records only a completed exact-callback query response, never an early handled or skipped pipeline.</summary>
    public ValueTask HandleAsync(OpenIddictServerEvents.ApplyAuthorizationResponseContext context) => NativeIssuanceProof.Find(context.Transaction) is { } proof
        ? proof.FormatAsync(context, () => native.HandleAsync(context)) : !string.IsNullOrEmpty(context.RedirectUri) && NativeErrorFormatter.Find(context.Transaction) is { } error
            ? error.ObserveAsync(context, NativeErrorFormat.Query, () => native.HandleAsync(context)) : native.HandleAsync(context);
}
