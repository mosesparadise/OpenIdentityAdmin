using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using OpenIddict.Client;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Persistence.Tests.Features.ProtocolStorage;

/// <summary>Retains only in-memory fault controls; credentials and native state never appear in diagnostic output.</summary>
internal sealed class NativeClientStateFault
{
    internal enum Kind
    {
        None, PresetCrypto, ChangedStoredPayload, ChangedCleanupPrincipal, ChangedDescriptorNonce, RemovedEncryption, ChangedDescriptorType,
        ChangedDescriptorClaims, ChangedHandler, ChangedCleanupParameters, RemovedCleanupState, ChangedLatePayload, ChangedLateReference
    }
    internal Kind Mode
    {
        get; set;
    }
    internal OpenIddictClientEvents.ProcessChallengeContext? Challenge
    {
        get; set;
    }
    internal string? PendingIdentifier
    {
        get; set;
    }
    internal int Calls
    {
        get; set;
    }
}

/// <summary>Injects a preset token before the actual native cryptographic handler can take its skip path.</summary>
internal sealed class NativeClientStateFaultHandler(NativeClientStateFault fault, NativeClientStateCleanup cleanup)
    : IOpenIddictClientHandler<OpenIddictClientEvents.GenerateTokenContext>
{
    private const string Preset = "synthetic-preset-state";
    /// <summary>Resolves the scoped cleanup witness and changes only the requested actual generation boundary.</summary>
    public ValueTask HandleAsync(OpenIddictClientEvents.GenerateTokenContext context)
    {
        GC.KeepAlive(cleanup);
        if (fault.Mode == NativeClientStateFault.Kind.PresetCrypto)
        {
            fault.Calls++;
            context.Token = Preset;
        }
        NativeClientStateCryptoFault.Apply(context, fault);
        return ValueTask.CompletedTask;
    }
}

/// <summary>Changes the persisted payload directly after native update, leaving the EF tracked entity unchanged.</summary>
internal sealed class NativeClientStateStoredFault(NativeClientStateFault fault, ProtocolStorageOwner owner)
    : IOpenIddictClientHandler<OpenIddictClientEvents.GenerateTokenContext>
{
    private const string Changed = "synthetic-persisted-payload-change";
    /// <summary>Proves final verification reloads durable state instead of trusting the native manager's tracked object.</summary>
    public async ValueTask HandleAsync(OpenIddictClientEvents.GenerateTokenContext context)
    {
        if (fault.Mode is NativeClientStateFault.Kind.ChangedLatePayload or NativeClientStateFault.Kind.ChangedLateReference)
        {
            fault.PendingIdentifier = context.Principal.GetTokenId();
        }
        if (fault.Mode == NativeClientStateFault.Kind.ChangedStoredPayload)
        {
            var identifier = context.Principal.GetTokenId();
            fault.Calls++;
            await owner.RunAsync(async token =>
                Assert.Equal(1, await owner.Database.Set<OpenIddictEntityFrameworkCoreToken>().Where(row => row.Id == identifier)
                    .ExecuteUpdateAsync(update => update.SetProperty(row => row.Payload, Changed), token)), context.CancellationToken, write: true);
        }
    }
}

/// <summary>Mutates continuation identity during actual child-scope disposal, after commit but before the caller resumes.</summary>
internal sealed class NativeClientStateCleanup(NativeClientStateFault fault) : IAsyncDisposable
{
    /// <summary>Requires the post-cleanup check to reject a replaced principal while leaving the committed native row consumed by capacity.</summary>
    public ValueTask DisposeAsync()
    {
        if (fault.Mode == NativeClientStateFault.Kind.ChangedCleanupPrincipal && fault.Challenge is { } challenge)
        {
            fault.Calls++;
            challenge.StateTokenPrincipal = challenge.StateTokenPrincipal!.Clone();
        }
        if (fault.Challenge is { } current && fault.Mode is NativeClientStateFault.Kind.ChangedCleanupParameters or NativeClientStateFault.Kind.RemovedCleanupState)
        {
            fault.Calls++;
            if (fault.Mode == NativeClientStateFault.Kind.ChangedCleanupParameters)
            {
                current.Parameters[OpenIddictConstants.Parameters.State] = OpenIddictConstants.Errors.InvalidRequest;
            }
            else
            {
                current.IncludeStateToken = false;
            }
        }
        return ValueTask.CompletedTask;
    }
}
