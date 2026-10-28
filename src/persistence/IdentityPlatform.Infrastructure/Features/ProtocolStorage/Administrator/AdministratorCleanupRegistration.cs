namespace IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;

/// <summary>Opaque deny-only B3 registration evidence minted by an actual locked read and usable only in that exact owning write phase.</summary>
internal sealed class AdministratorCleanupRegistration
{
    private readonly ProtocolStorageScope scope;
    private readonly ProtocolStoragePhase phase;
    private readonly ProtocolStorageRequest request;
    internal string ApplicationId
    {
        get;
    }
    internal string ScopeId
    {
        get;
    }
    internal long Revision
    {
        get;
    }
    internal long ObservedAt
    {
        get;
    }

    /// <summary>Copies immutable identity without exposing a constructor that accepts issuance records or caller-selected eligibility flags.</summary>
    private AdministratorCleanupRegistration(ProtocolStorageScope scope, ProtocolStoragePhase phase, AdministratorRegistrationBinding binding)
    {
        this.scope = scope;
        this.phase = phase;
        request = ProtocolStorageAmbient.Current?.Request ?? throw ProtocolStorageOwner.Unavailable();
        ApplicationId = binding.ApplicationId;
        ScopeId = binding.ScopeId;
        Revision = binding.Revision;
        ObservedAt = binding.ObservedAt;
    }

    /// <summary>Requires exact B3 metadata and unchanged ownership across the real read; central issuance disablement is the sole exception.</summary>
    internal static async ValueTask<AdministratorCleanupRegistration?> ReadAsync(ProtocolStorageScope scope,
        AdministratorRegistrationPolicy expected, CancellationToken token)
    {
        var phase = scope.Owner.Capture(write: true);
        var binding = await AdministratorRegistration.ReadCleanupStateAsync(scope, expected, token);
        scope.Owner.Check(phase, token);
        if (binding is null)
        {
            return null;
        }
        var result = new AdministratorCleanupRegistration(scope, phase, binding);
        result.RequireCurrent(scope.Owner, token);
        return result;
    }

    /// <summary>Rejects old, foreign, completed or disposed owners before the ledger can tolerate a disabled profile.</summary>
    internal void RequireCurrent(ProtocolStorageOwner owner, CancellationToken token)
    {
        request.RequireCurrent();
        if (!ReferenceEquals(scope.Owner, owner) || !ReferenceEquals(scope, ProtocolStorageAmbient.Current?.Scope) ||
            !ReferenceEquals(request, ProtocolStorageAmbient.Current?.Request))
        {
            request.Fail();
            scope.Owner.Fail();
            owner.Fail();
            throw ProtocolStorageOwner.Unavailable();
        }
        owner.Check(phase, token);
    }
}
