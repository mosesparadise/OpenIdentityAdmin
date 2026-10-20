using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using Microsoft.IdentityModel.JsonWebTokens;
using OpenIddict.Abstractions;
using OpenIddict.Client;

namespace IdentityPlatform.Admin.Bff.Features.Authentication.Ownership;

/// <summary>Observes the actual native create, crypto and payload handlers for one reference-state generation.</summary>
internal sealed class NativeStateGeneration
{
    private static readonly string Key = Guid.NewGuid().ToString();
    private readonly ProtocolStorageScope scope;
    private readonly ProtocolStorageRequest request;
    private readonly ProtocolStoragePhase phase;
    private readonly OpenIddictClientEvents.ProcessChallengeContext challenge;
    private readonly NativeStateBinding binding;
    private OpenIddictClientEvents.GenerateTokenContext? generation;
    private ProtocolTokenPreparation? preparation;
    private string? identifier;
    private string? claims;
    private string? payload;
    private string? reference;
    private bool completed;

    /// <summary>Attaches once to the real native transaction only while the fresh Sessions write phase is active.</summary>
    internal NativeStateGeneration(ProtocolStorageScope scope, OpenIddictClientEvents.ProcessChallengeContext challenge, NativeStateBinding binding)
    {
        this.scope = scope;
        this.challenge = challenge;
        this.binding = binding;
        request = ProtocolStorageAmbient.Current?.Request ?? throw ProtocolStorageOwner.Unavailable();
        phase = scope.Owner.Capture(write: true);
        if (Find(challenge.Transaction) is not null)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        challenge.Transaction.SetProperty(Key, this);
    }

    /// <summary>Reads only the private proof attached to the actual challenge transaction.</summary>
    internal static NativeStateGeneration? Find(OpenIddictClientTransaction transaction) => transaction.GetProperty<NativeStateGeneration>(Key);

    /// <summary>Observes the whole exact nested generation, refusing early handled/skipped or repeated dispatch completion.</summary>
    internal async ValueTask GenerateAsync(OpenIddictClientEvents.GenerateTokenContext current, Func<ValueTask> native)
    {
        if (generation is not null || !ReferenceEquals(current.Transaction, challenge.Transaction))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        generation = current;
        preparation = new(current.SecurityTokenDescriptor, current.Options.JsonWebTokenHandler, typeof(JsonWebTokenHandler), encrypted: true,
            current.Options.SigningCredentials, current.Options.EncryptionCredentials);
        await ObserveAsync(current, async () =>
        {
            if (!string.IsNullOrEmpty(current.Token) || !string.IsNullOrEmpty(current.Principal.GetTokenId()))
            {
                throw ProtocolStorageOwner.Unavailable();
            }
            await native();
            if (identifier is null || payload is null || reference is null || current.Token != reference || claims != NativeStateBinding.Claims(current.Principal))
            {
                throw ProtocolStorageOwner.Unavailable();
            }
            completed = true;
        });
    }

    /// <summary>Observes the actual native credential, subject and metadata population slots.</summary>
    internal ValueTask PrepareAsync(OpenIddictClientEvents.GenerateTokenContext current, ProtocolTokenPreparation.Stage next, Func<ValueTask> native) =>
        ObserveAsync(current, () => preparation!.AdvanceAsync(next, current.SecurityTokenDescriptor, () => current.SecurityTokenHandler, native));

    /// <summary>Allows the installed client key policy to select active material while retaining previous validation/decryption keys.</summary>
    internal ValueTask RefreshCredentialsAsync(OpenIddictClientEvents.GenerateTokenContext current, Func<ValueTask> native) =>
        ObserveAsync(current, () => preparation!.RefreshCredentialsAsync(current.SecurityTokenDescriptor, () => current.SecurityTokenHandler, native));

    /// <summary>Records the actual native row identity before cryptographic generation may begin.</summary>
    internal ValueTask CreateAsync(OpenIddictClientEvents.GenerateTokenContext current, Func<ValueTask> native) => ObserveAsync(current, async () =>
    {
        if (identifier is not null || !string.IsNullOrEmpty(current.Principal.GetTokenId()) || !string.IsNullOrEmpty(current.Token))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        await native();
        identifier = current.Principal.GetTokenId();
        if (string.IsNullOrEmpty(identifier))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
    });

    /// <summary>Rejects preset output so the native crypto handler cannot silently take its intentional skip path.</summary>
    internal ValueTask SignAsync(OpenIddictClientEvents.GenerateTokenContext current, Func<ValueTask> native) => ObserveAsync(current, async () =>
    {
        preparation!.RequireReady(current.SecurityTokenDescriptor, current.SecurityTokenHandler);
        if (identifier is null || payload is not null || !string.IsNullOrEmpty(current.Token))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        await native();
        payload = current.Token;
        claims = NativeStateBinding.Claims(current.Principal);
        if (string.IsNullOrEmpty(payload))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
    });

    /// <summary>Observes exact protected output becoming the real native hashed reference and persisted payload.</summary>
    internal ValueTask PersistAsync(OpenIddictClientEvents.GenerateTokenContext current, Func<ValueTask> native) => ObserveAsync(current, async () =>
    {
        if (payload is null || reference is not null || current.Token != payload || claims != NativeStateBinding.Claims(current.Principal))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        await native();
        reference = current.Token;
        if (string.IsNullOrEmpty(reference) || reference == payload)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
    });

    /// <summary>Requires every observation and final continuation binding after native return and after child resource disposal.</summary>
    internal void RequireCompleted()
    {
        request.RequireCurrent();
        binding.RequireCurrent();
        if (generation is null || preparation is null)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        preparation.RequireReady(generation.SecurityTokenDescriptor, generation.SecurityTokenHandler);
        if (!completed || generation is null || generation.IsRejected || generation.IsRequestHandled || generation.IsRequestSkipped ||
            !generation.CreateTokenEntry || !generation.IsReferenceToken || !generation.PersistTokenPayload ||
            generation.TokenType != OpenIddictConstants.TokenTypeIdentifiers.Private.StateToken ||
            generation.TokenFormat != OpenIddictConstants.TokenFormats.Private.JsonWebToken ||
            !ReferenceEquals(generation.Principal, challenge.StateTokenPrincipal) || reference != challenge.StateToken || reference != generation.Token ||
            identifier != challenge.StateTokenPrincipal!.GetTokenId() || claims != NativeStateBinding.Claims(challenge.StateTokenPrincipal))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
    }

    /// <summary>Ends only this generation observer after child cleanup so later intentional client assertions retain their native pipeline.</summary>
    internal void Release()
    {
        RequireCompleted();
        if (!phase.Committed || !phase.CleanupCompleted || !scope.ActualCompletion.IsCompletedSuccessfully ||
            !ReferenceEquals(Find(challenge.Transaction), this) || !challenge.Transaction.Properties.Remove(Key))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
    }

    /// <summary>Checks the authoritative reloaded row's payload against the exact completed native crypto output.</summary>
    internal bool MatchesPayload(string? value) => completed && payload is not null && value == payload;

    /// <summary>Latches native observation failures so an intervening handler cannot catch an exception and restore success.</summary>
    private async ValueTask ObserveAsync(OpenIddictClientEvents.GenerateTokenContext current, Func<ValueTask> native)
    {
        try
        {
            RequireCurrent(current);
            await native();
            RequireCurrent(current);
        }
        catch { request.Fail(); scope.Owner.Fail(); throw; }
    }

    /// <summary>Checks exact native context, principal, settings and active owner for every nested handler observation.</summary>
    private void RequireCurrent(OpenIddictClientEvents.GenerateTokenContext current)
    {
        binding.RequireCurrent();
        preparation!.RequireCurrent(current.SecurityTokenDescriptor, current.SecurityTokenHandler);
        scope.Owner.Check(phase, current.CancellationToken);
        if (!ReferenceEquals(ProtocolStorageAmbient.Current?.Request, request) || !ReferenceEquals(ProtocolStorageAmbient.Current?.Scope, scope) ||
            !ReferenceEquals(current, generation) || !ReferenceEquals(current.Principal, challenge.StateTokenPrincipal) ||
            !current.CreateTokenEntry || !current.IsReferenceToken || !current.PersistTokenPayload ||
            current.TokenType != OpenIddictConstants.TokenTypeIdentifiers.Private.StateToken ||
            current.TokenFormat != OpenIddictConstants.TokenFormats.Private.JsonWebToken ||
            current.IsRejected || current.IsRequestHandled || current.IsRequestSkipped)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
    }
}
