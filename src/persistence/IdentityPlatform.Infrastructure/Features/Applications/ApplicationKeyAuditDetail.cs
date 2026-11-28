using IdentityPlatform.Database;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Infrastructure.Features.Applications;

/// <summary>Retains the approved public fingerprint and exact credential transition for each accepted administrator key event.</summary>
internal sealed class ApplicationKeyAuditDetail
{
    public Guid AuditId
    {
        get; init;
    }
    public long PreviousRevision
    {
        get; init;
    }
    public long CredentialRevision
    {
        get; init;
    }
    public string Fingerprint { get; init; } = string.Empty;
    private const int FingerprintLength = 64;
    private const string Constraint = "CK_ApplicationKeyAuditDetail_Revision";
    private const string Revisions = @"""PreviousRevision"" >= 0 AND ""CredentialRevision"" > ""PreviousRevision"" AND ""CredentialRevision"" - ""PreviousRevision"" = 1";

    /// <summary>Binds one revision transition to its accepted audit intent without retaining submitted key JSON.</summary>
    internal static void Configure(ModelBuilder builder, IDatabaseModelDialect dialect)
    {
        var row = builder.Entity<ApplicationKeyAuditDetail>();
        row.HasKey(value => value.AuditId);
        row.Property(value => value.Fingerprint).HasMaxLength(FingerprintLength).IsUnicode(false);
        row.HasOne<SecurityAuditIntent>().WithOne().HasForeignKey<ApplicationKeyAuditDetail>(value => value.AuditId).OnDelete(DeleteBehavior.Cascade);
        row.ToTable(nameof(ApplicationKeyAuditDetail), table => table.HasCheckConstraint(Constraint, dialect.Identifiers(Revisions)));
    }
}
