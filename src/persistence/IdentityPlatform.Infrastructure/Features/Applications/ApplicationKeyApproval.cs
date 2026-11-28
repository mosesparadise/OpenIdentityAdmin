using IdentityPlatform.Core.Features.Applications;
using IdentityPlatform.Database;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace IdentityPlatform.Infrastructure.Features.Applications;

/// <summary>Pins one locally approved public RSA key to a managed application's current credential revision.</summary>
internal sealed class ApplicationKeyApproval
{
    internal const string MachineProfile = "managed-machine-key-v1";
    internal const string WebProfile = "managed-web-key-v1";
    private const int FingerprintLength = 64;
    private const string RevisionCheck = "CK_ApplicationKeyApproval_Revision";
    private const string PositiveRevision = @"""CredentialRevision"" > 0";

    public string ApplicationId { get; private set; } = string.Empty;
    public long CredentialRevision
    {
        get; private set;
    }
    public string Fingerprint { get; private set; } = string.Empty;

    public string Thumbprint { get; private set; } = string.Empty;

    /// <summary>Records a public-only fingerprint for the selected policy and never retains the submitted JSON or private key material.</summary>
    internal static ApplicationKeyApproval Create(ApplicationPolicy policy, AdministratorPublicKey key)
    {
        if (policy.Profile is not (MachineProfile or WebProfile) || policy.CredentialRevision <= 0 || policy.Retired)
        {
            throw new InvalidOperationException();
        }
        return new()
        {
            ApplicationId = policy.ApplicationId,
            CredentialRevision = policy.CredentialRevision,
            Fingerprint = key.Fingerprint,
            Thumbprint = Material(key)
        };
    }

    /// <summary>Replaces the fingerprint only for the same key-authenticated registration and its next credential revision.</summary>
    internal void Replace(ApplicationPolicy successor, AdministratorPublicKey replacement)
    {
        if (successor.ApplicationId != ApplicationId || CredentialRevision == long.MaxValue ||
            successor.CredentialRevision != CredentialRevision + 1 || Material(replacement) == Thumbprint)
        {
            throw new InvalidOperationException();
        }
        var next = Create(successor, replacement);
        CredentialRevision = next.CredentialRevision;
        Fingerprint = next.Fingerprint;
        Thumbprint = next.Thumbprint;
    }

    /// <summary>Binds native signature validation to the current approved public bytes, descriptor and credential lifetime.</summary>
    internal bool Matches(ApplicationPolicy policy, AdministratorPublicKey current, SecurityKey validatedKey, long now) =>
        validatedKey.CanComputeJwkThumbprint() && Matches(policy, current, validatedKey.KeyId, Convert.ToHexStringLower(validatedKey.ComputeJwkThumbprint()), now);

    /// <summary>Checks the immutable public-key evidence captured when the native validator completed.</summary>
    internal bool Matches(ApplicationPolicy policy, AdministratorPublicKey current, string? keyId, string thumbprint, long now) =>
        policy.Profile is MachineProfile or WebProfile && policy.Enabled && !policy.Retired && policy.ApplicationId == ApplicationId &&
        policy.CredentialRevision == CredentialRevision && policy.CredentialIssuedAt <= now && policy.ObservedAt <= now && policy.CredentialExpiresAt > now &&
        current.Fingerprint == Fingerprint && Material(current) == Thumbprint && current.KeyId == keyId && Thumbprint == thumbprint;

    /// <summary>Uses the RFC 7638 RSA public key thumbprint independently of mutable identifier and algorithm labels.</summary>
    private static string Material(AdministratorPublicKey key) => Convert.ToHexStringLower(key.CreateNativeKey().ComputeJwkThumbprint());

    /// <summary>Requires an existing retained application and a single approval per identity; revision and fingerprint are concurrency preconditions.</summary>
    internal static void Configure(ModelBuilder builder, IDatabaseModelDialect dialect)
    {
        var row = builder.Entity<ApplicationKeyApproval>();
        row.HasKey(value => value.ApplicationId);
        row.Property(value => value.ApplicationId).HasMaxLength(ApplicationProjection.IdentifierLength);
        row.Property(value => value.CredentialRevision).IsConcurrencyToken();
        row.Property(value => value.Fingerprint).HasMaxLength(FingerprintLength).IsConcurrencyToken();
        row.Property(value => value.Thumbprint).HasMaxLength(FingerprintLength).IsUnicode(false).IsConcurrencyToken();
        row.HasOne<ApplicationPolicy>().WithMany().HasForeignKey(value => value.ApplicationId).OnDelete(DeleteBehavior.Restrict);
        row.ToTable(nameof(ApplicationKeyApproval), table => table.HasCheckConstraint(RevisionCheck, dialect.Identifiers(PositiveRevision)));
        if (dialect.IdentityOrdinalCollation is { } ordinal)
        {
            row.Property(value => value.ApplicationId).UseCollation(ordinal);
            row.Property(value => value.Fingerprint).UseCollation(ordinal);
        }
    }
}
