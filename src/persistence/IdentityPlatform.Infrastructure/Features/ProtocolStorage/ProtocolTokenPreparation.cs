using Microsoft.IdentityModel.Tokens;

namespace IdentityPlatform.Infrastructure.Features.ProtocolStorage;

/// <summary>Observes only actual native preparation stages, then freezes the descriptor and crypto handler through publication.</summary>
internal sealed class ProtocolTokenPreparation
{
    internal enum Stage
    {
        Empty, Credentials, Subject, Metadata
    }
    private readonly SecurityTokenDescriptor descriptor;
    private readonly object handler;
    private readonly bool encrypted;
    private readonly SigningCredentials[] signing;
    private readonly EncryptingCredentials[] encryption;
    private ProtocolTokenDescriptorSnapshot snapshot;
    private Stage stage;
    private bool refreshed;

    /// <summary>Starts with the actual default descriptor and explicitly selected installed handler/encryption policy.</summary>
    internal ProtocolTokenPreparation(SecurityTokenDescriptor descriptor, object handler, Type handlerType, bool encrypted,
        IEnumerable<SigningCredentials> signing, IEnumerable<EncryptingCredentials> encryption)
    {
        if (handler.GetType() != handlerType)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        ProtocolTokenDescriptorSnapshot.RequireEmpty(descriptor);
        this.descriptor = descriptor;
        this.handler = handler;
        this.encrypted = encrypted;
        this.signing = signing.ToArray();
        this.encryption = encryption.ToArray();
        snapshot = new(descriptor);
    }

    /// <summary>Allows exactly the installed credentials, subject and metadata stages, in order, without permitting intervening mutations.</summary>
    internal async ValueTask AdvanceAsync(Stage next, SecurityTokenDescriptor current, Func<object> currentHandler, Func<ValueTask> native)
    {
        RequireCurrent(current, currentHandler());
        if ((int)next != (int)stage + 1 || next > Stage.Metadata)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        await native();
        RequireIdentity(current, currentHandler());
        stage = next;
        snapshot = new(current);
    }

    /// <summary>Permits the issuer's real active-key policy to refine native credentials once before subject preparation.</summary>
    internal async ValueTask RefreshCredentialsAsync(SecurityTokenDescriptor current, Func<object> currentHandler, Func<ValueTask> native)
    {
        RequireCurrent(current, currentHandler());
        if (stage != Stage.Credentials || refreshed)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        await native();
        RequireIdentity(current, currentHandler());
        snapshot = new(current);
        refreshed = true;
    }

    /// <summary>Freezes each completed native stage while allowing the initial unassigned handler.</summary>
    internal void RequireCurrent(SecurityTokenDescriptor current, object? currentHandler)
    {
        snapshot.RequireCurrent(current);
        if (stage == Stage.Empty)
        {
            if (currentHandler is not null)
            {
                throw ProtocolStorageOwner.Unavailable();
            }
        }
        else
        {
            RequireIdentity(current, currentHandler);
        }
    }

    /// <summary>Rejects omitted native stages, descriptor substitution, unsigned output or unintended encryption changes.</summary>
    internal void RequireReady(SecurityTokenDescriptor current, object currentHandler)
    {
        if (stage != Stage.Metadata)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        RequireIdentity(current, currentHandler);
        snapshot.RequireCurrent(current);
    }

    /// <summary>Checks only data identity while allowing the native stage currently executing to populate its own fields.</summary>
    private void RequireIdentity(SecurityTokenDescriptor current, object? currentHandler)
    {
        if (!ReferenceEquals(descriptor, current) || !ReferenceEquals(handler, currentHandler) || current.SigningCredentials is null ||
            (current.EncryptingCredentials is not null) != encrypted || !signing.Any(value => ReferenceEquals(value, current.SigningCredentials)) ||
            encrypted && !encryption.Any(value => ReferenceEquals(value, current.EncryptingCredentials)))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
    }
}
