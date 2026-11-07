using IdentityPlatform.Core.Features.Applications;
using IdentityPlatform.Database;
using IdentityPlatform.Infrastructure.Features.Applications;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Infrastructure.Features.ApplicationGrants;

/// <summary>Adds portable job and immutable approval/audit mappings without altering native protocol history.</summary>
internal static class ApplicationGrantModel
{
    private const string ApplicationGrantJobStateConstraint = "CK_ApplicationGrantJob_State";
    private const string ApplicationGrantJobCountConstraint = "CK_ApplicationGrantJob_Count";
    private const string ApplicationGrantJobRevisionConstraint = "CK_ApplicationGrantJob_Revision";
    private const string ApplicationGrantJobTimeConstraint = "CK_ApplicationGrantJob_Time";
    private const string ApplicationGrantApprovalIntentConstraint = "CK_ApplicationGrantApproval_Intent";
    private const string ApplicationGrantApprovalTimeConstraint = "CK_ApplicationGrantApproval_Time";
    private const string ApplicationGrantAuditStateConstraint = "CK_ApplicationGrantAudit_State";
    private const string ApplicationGrantAuditCountConstraint = "CK_ApplicationGrantAudit_Count";
    private const string ApplicationGrantAuditDeltaConstraint = "CK_ApplicationGrantAudit_Delta";

    private const int DigestLength = 64;
    private const string StateSql = "\"State\" IN (1, 2, 3, 4) AND \"Failure\" IN (0, 1, 2, 3, 4) AND ((\"State\" = 3 AND \"Failure\" <> 0) OR (\"State\" <> 3 AND \"Failure\" = 0))";
    private const string CountSql = "\"AuthorizationCount\" >= 0 AND \"TokenCount\" >= 0";
    private const string JobRevisionSql = "\"Revision\" > 0 AND \"ApplicationRevision\" > 0";
    private const string JobTimeSql = "\"CreatedAt\" >= 0 AND \"ObservedAt\" >= \"CreatedAt\"";
    private const string ApprovalSql = "\"Action\" IN (1, 2) AND \"Reason\" IN (1, 2, 3) AND \"ApplicationRevision\" > 0";
    private const string ApprovalTimeSql = "\"FactorVerifiedAt\" >= 0 AND \"ApprovedAt\" >= \"FactorVerifiedAt\" AND \"ReplayUntil\" > \"ApprovedAt\"";
    private const string DeltaSql = "\"JobRevision\" > 0 AND \"AuthorizationDelta\" >= 0 AND \"TokenDelta\" >= 0 AND \"AuthorizationDelta\" <= \"AuthorizationCount\" AND \"TokenDelta\" <= \"TokenCount\"";

    /// <summary>Registers cohesive feature tables and explicit restricted relationships on either selected relational provider.</summary>
    internal static void Configure(ModelBuilder builder, IDatabaseModelDialect dialect)
    {
        Job(builder, dialect);
        Approval(builder, dialect);
        Audit(builder, dialect);
    }

    /// <summary>Indexes bounded polling and exact application lookup; governance serialization enforces one incomplete operation.</summary>
    private static void Job(ModelBuilder builder, IDatabaseModelDialect dialect)
    {
        var row = builder.Entity<ApplicationGrantJob>();
        row.HasKey(value => value.Id);
        row.Property(value => value.ApplicationId).HasMaxLength(ApplicationProjection.IdentifierLength);
        row.Property(value => value.Revision).IsConcurrencyToken();
        row.Property(value => value.State).HasConversion<int>();
        row.Property(value => value.Failure).HasConversion<int>();
        row.HasIndex(value => new { value.State, value.ObservedAt, value.Id });
        row.HasIndex(value => new { value.ApplicationId, value.State });
        row.HasOne<ApplicationPolicy>().WithMany().HasForeignKey(value => value.ApplicationId).OnDelete(DeleteBehavior.Restrict);
        row.ToTable(nameof(ApplicationGrantJob), table =>
        {
            table.HasCheckConstraint(ApplicationGrantJobStateConstraint, dialect.Identifiers(StateSql));
            table.HasCheckConstraint(ApplicationGrantJobCountConstraint, dialect.Identifiers(CountSql));
            table.HasCheckConstraint(ApplicationGrantJobRevisionConstraint, dialect.Identifiers(JobRevisionSql));
            table.HasCheckConstraint(ApplicationGrantJobTimeConstraint, dialect.Identifiers(JobTimeSql));
        });
        if (dialect.IdentityOrdinalCollation is { } ordinal)
        {
            row.Property(value => value.ApplicationId).UseCollation(ordinal);
        }
    }

    /// <summary>Uses a bounded digest index rather than exceeding SQL Server index width with actor and application identifiers.</summary>
    private static void Approval(ModelBuilder builder, IDatabaseModelDialect dialect)
    {
        var row = builder.Entity<ApplicationGrantApproval>();
        row.HasKey(value => value.Id);
        row.Property(value => value.ActorId).HasMaxLength(FactorSchema.UserLength);
        row.Property(value => value.ApplicationId).HasMaxLength(ApplicationProjection.IdentifierLength);
        row.Property(value => value.ScopedKeyHash).HasMaxLength(DigestLength);
        row.Property(value => value.RequestHash).HasMaxLength(DigestLength);
        row.Property(value => value.Action).HasConversion<int>();
        row.Property(value => value.Reason).HasConversion<int>();
        row.HasIndex(value => value.ScopedKeyHash).IsUnique();
        row.HasIndex(value => value.JobId);
        row.HasOne<ApplicationGrantJob>().WithMany().HasForeignKey(value => value.JobId).OnDelete(DeleteBehavior.Restrict);
        row.HasOne<SecurityAuditIntent>().WithOne().HasForeignKey<ApplicationGrantApproval>(value => value.AuditId).OnDelete(DeleteBehavior.Restrict);
        row.ToTable(nameof(ApplicationGrantApproval), table =>
        {
            table.HasCheckConstraint(ApplicationGrantApprovalIntentConstraint, dialect.Identifiers(ApprovalSql));
            table.HasCheckConstraint(ApplicationGrantApprovalTimeConstraint, dialect.Identifiers(ApprovalTimeSql));
        });
        if (dialect.IdentityOrdinalCollation is { } ordinal)
        {
            row.Property(value => value.ActorId).UseCollation(ordinal);
            row.Property(value => value.ApplicationId).UseCollation(ordinal);
            row.Property(value => value.ScopedKeyHash).UseCollation(ordinal);
            row.Property(value => value.RequestHash).UseCollation(ordinal);
        }
    }

    /// <summary>Prevents orphaned progress receipts and duplicate revisions while retaining exact committed deltas.</summary>
    private static void Audit(ModelBuilder builder, IDatabaseModelDialect dialect)
    {
        var row = builder.Entity<ApplicationGrantAudit>();
        row.HasKey(value => value.AuditId);
        row.Property(value => value.State).HasConversion<int>();
        row.Property(value => value.Failure).HasConversion<int>();
        row.HasIndex(value => new { value.JobId, value.JobRevision }).IsUnique();
        row.HasOne<SecurityAuditIntent>().WithOne().HasForeignKey<ApplicationGrantAudit>(value => value.AuditId).OnDelete(DeleteBehavior.Restrict);
        row.HasOne<ApplicationGrantJob>().WithMany().HasForeignKey(value => value.JobId).OnDelete(DeleteBehavior.Restrict);
        row.HasOne<ApplicationGrantApproval>().WithMany().HasForeignKey(value => value.ApprovalId).OnDelete(DeleteBehavior.Restrict);
        row.ToTable(nameof(ApplicationGrantAudit), table =>
        {
            table.HasCheckConstraint(ApplicationGrantAuditStateConstraint, dialect.Identifiers(StateSql));
            table.HasCheckConstraint(ApplicationGrantAuditCountConstraint, dialect.Identifiers(CountSql));
            table.HasCheckConstraint(ApplicationGrantAuditDeltaConstraint, dialect.Identifiers(DeltaSql));
        });
    }
}
