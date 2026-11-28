using IdentityPlatform.Core.Features.Applications;
using IdentityPlatform.Database;
using IdentityPlatform.Infrastructure.Features.Identity.ManagedApplications;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Infrastructure.Features.Applications;

/// <summary>Retains an assertion's one-use digest per permanent managed application across credential replacements.</summary>
internal sealed class ApplicationAssertionUse
{
    private const int DigestLength = 64;
    private const string TimeCheck = "CK_ApplicationAssertionUse_Time";
    private const string TimeSql = @"""CredentialRevision"" > 0 AND ""RetainUntil"" > 0";
    public string ApplicationId { get; private set; } = string.Empty;
    public string Digest { get; private set; } = string.Empty;
    public long CredentialRevision
    {
        get; private set;
    }
    public long RetainUntil
    {
        get; private set;
    }

    /// <summary>Creates replay state only for the exact validated key and current credential, using the fixed native assertion time and identifier limits.</summary>
    internal static ApplicationAssertionUse? FromValidated(ApplicationPolicy policy, ApplicationKeyApproval approval, AdministratorPublicKey approvedKey,
        Microsoft.IdentityModel.Tokens.SecurityKey validatedKey, string jti, long issued, long expires, long? notBefore, long now)
    {
        return approval.Matches(policy, approvedKey, validatedKey, now) ? Create(policy, approval, jti, issued, expires, notBefore, now) : null;
    }

    /// <summary>Uses the signed claims and key thumbprint captured together during native validation.</summary>
    internal static ApplicationAssertionUse? FromProof(ApplicationPolicy policy, ApplicationKeyApproval approval, AdministratorPublicKey approvedKey,
        SignedClientAssertion proof, long now) => approval.Matches(policy, approvedKey, proof.KeyId, proof.KeyThumbprint, now)
            ? Create(policy, approval, proof.Identifier, proof.Issued, proof.Expires, proof.NotBefore, now) : null;

    /// <summary>Checks the integer lifetime before hashing the original identifier and retaining the replay deadline.</summary>
    private static ApplicationAssertionUse? Create(ApplicationPolicy policy, ApplicationKeyApproval approval, string jti, long issued, long expires, long? notBefore, long now) =>
        ClientAssertionLimits.Valid(jti, issued, expires, notBefore) && ClientAssertionLimits.Allows(issued, expires, notBefore, now)
            ? new()
            {
                ApplicationId = policy.ApplicationId,
                Digest = ManagedLoginRules.Digest(jti),
                CredentialRevision = approval.CredentialRevision,
                RetainUntil = expires + ClientAssertionLimits.ClockSkewSeconds
            } : null;

    /// <summary>Uses the application ID and assertion digest as the unique key; key replacement cannot reset accepted assertion IDs.</summary>
    internal static void Configure(ModelBuilder builder, IDatabaseModelDialect dialect)
    {
        var row = builder.Entity<ApplicationAssertionUse>();
        row.HasKey(value => new { value.ApplicationId, value.Digest });
        row.Property(value => value.ApplicationId).HasMaxLength(ApplicationProjection.IdentifierLength);
        row.Property(value => value.Digest).HasMaxLength(DigestLength).IsUnicode(false);
        row.HasOne<ApplicationPolicy>().WithMany().HasForeignKey(value => value.ApplicationId).OnDelete(DeleteBehavior.Restrict);
        row.ToTable(nameof(ApplicationAssertionUse), table => table.HasCheckConstraint(TimeCheck, dialect.Identifiers(TimeSql)));
        row.HasIndex(value => value.RetainUntil);
        if (dialect.IdentityOrdinalCollation is { } ordinal)
        {
            row.Property(value => value.ApplicationId).UseCollation(ordinal);
            row.Property(value => value.Digest).UseCollation(ordinal);
        }
    }
}
