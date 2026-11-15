using IdentityPlatform.Core.Features.Applications;
using IdentityPlatform.Database;
using IdentityPlatform.Infrastructure.Features.ApplicationGrants;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Infrastructure.Features.Applications;

/// <summary>Constrains immutable retirement provenance and restrictive retained references equally on both relational providers.</summary>
internal static class ApplicationRetirementModel
{
    private const string RevisionConstraint = "CK_ApplicationRetirement_Revision";
    private const string ReasonConstraint = "CK_ApplicationRetirement_Reason";
    private const string TimeConstraint = "CK_ApplicationRetirement_Time";
    private const string RevisionSql = "\"OriginalRevision\" > 0 AND \"RetiredRevision\" - \"OriginalRevision\" = 1";
    private const string ReasonSql = "\"Reason\" IN (1, 2, 3)";
    private const string TimeSql = "\"RetiredAt\" >= 0 AND \"ReplayUntil\" > \"RetiredAt\" AND \"ReplayUntil\" - \"RetiredAt\" = 86400";

    /// <summary>Uses bounded ordinal digests and independent original job/approval links without cascading away security history.</summary>
    internal static void Configure(ModelBuilder builder, IDatabaseModelDialect dialect)
    {
        var row = builder.Entity<ApplicationRetirement>();
        row.HasKey(value => value.ApplicationId);
        FactorSchema.Text(row, nameof(ApplicationRetirement.ApplicationId), ApplicationProjection.IdentifierLength, dialect);
        FactorSchema.Text(row, nameof(ApplicationRetirement.ActorId), FactorSchema.UserLength, dialect);
        FactorSchema.Text(row, nameof(ApplicationRetirement.ScopedKeyHash), FactorSchema.DigestLength, dialect);
        FactorSchema.Text(row, nameof(ApplicationRetirement.RequestHash), FactorSchema.DigestLength, dialect);
        row.Property(value => value.Reason).HasConversion<int>();
        row.HasIndex(value => value.ScopedKeyHash).IsUnique();
        row.HasOne<ApplicationPolicy>().WithOne().HasForeignKey<ApplicationRetirement>(value => value.ApplicationId).OnDelete(DeleteBehavior.Restrict);
        row.HasOne<SecurityAuditIntent>().WithOne().HasForeignKey<ApplicationRetirement>(value => value.AuditId).OnDelete(DeleteBehavior.Restrict);
        row.HasOne<ApplicationGrantJob>().WithOne().HasForeignKey<ApplicationRetirement>(value => value.GrantJobId).OnDelete(DeleteBehavior.Restrict);
        row.HasOne<ApplicationGrantApproval>().WithOne().HasForeignKey<ApplicationRetirement>(value => value.GrantApprovalId).OnDelete(DeleteBehavior.Restrict);
        row.ToTable(nameof(ApplicationRetirement), table =>
        {
            table.HasCheckConstraint(RevisionConstraint, dialect.Identifiers(RevisionSql));
            table.HasCheckConstraint(ReasonConstraint, dialect.Identifiers(ReasonSql));
            table.HasCheckConstraint(TimeConstraint, dialect.Identifiers(TimeSql));
        });
    }
}
