using IdentityPlatform.AuthServer.Features.Protocol.Administrator;
using IdentityPlatform.AuthServer.Features.Protocol.ManagedApplications;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using OpenIddict.Server;

namespace IdentityPlatform.AuthServer.Features.Protocol.Ownership;

/// <summary>Routes existing native observation slots to mutually exclusive typed owners without sharing their publication capabilities.</summary>
internal static class NativeTokenObservations
{
    /// <summary>Preserves the actual nested native dispatcher and selects only a privately attached generation observer.</summary>
    internal static ValueTask GenerateAsync(OpenIddictServerEvents.GenerateTokenContext context, Func<ValueTask> native)
    {
        Exclusive(context);
        return NativeIssuanceProof.Find(context.Transaction) is { } administrator ? administrator.GenerateAsync(context, native) :
            ManagedCacheProof.Find(context.Transaction) is { } managed ? managed.GenerateAsync(context, native) : native();
    }

    /// <summary>Reuses low-level native token preparation/persistence evidence while preserving separate profile ownership checks.</summary>
    internal static ValueTask ObserveAsync(OpenIddictServerEvents.GenerateTokenContext context, Func<NativeIssuedToken, ValueTask> operation,
        Func<ValueTask> native)
    {
        Exclusive(context);
        return NativeIssuanceProof.Find(context.Transaction) is { } administrator ? administrator.ObserveAsync(context, operation) :
            ManagedCacheProof.Find(context.Transaction) is { } managed ? managed.ObserveAsync(context, operation) : native();
    }

    /// <summary>Rejects any attempt to combine managed cache and administrator issuance observations in one native transaction.</summary>
    private static void Exclusive(OpenIddictServerEvents.GenerateTokenContext context)
    {
        if (NativeIssuanceProof.Find(context.Transaction) is not null && ManagedCacheProof.Find(context.Transaction) is not null)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
    }
}
