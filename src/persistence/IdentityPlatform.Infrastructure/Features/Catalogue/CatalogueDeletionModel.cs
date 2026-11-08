using IdentityPlatform.Core.Features.Catalogue;
using IdentityPlatform.Database;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Infrastructure.Features.Catalogue;

/// <summary>Constrains durable deletion approval, idempotency and terminal evidence without cascading deletion of security history.</summary>
internal static class CatalogueDeletionModel
{
    private const string CatalogueDeletionStateConstraint = "CK_CatalogueDeletion_State";
    private const string CatalogueDeletionAuthorityConstraint = "CK_CatalogueDeletion_Authority";
    private const string CatalogueDeletionTimeConstraint = "CK_CatalogueDeletion_Time";
    private const string CatalogueDeletionTargetConstraint = "CK_CatalogueDeletion_Target";
    private const string StateSql = "\"State\" IN (1, 2, 3) AND \"Failure\" IN (0, 1, 2, 3, 4, 5) AND " +
        "((\"State\" = 3 AND \"Failure\" <> 0) OR (\"State\" <> 3 AND \"Failure\" = 0)) AND " +
        "((\"State\" = 1 AND \"CompletionAuditId\" IS NULL) OR (\"State\" <> 1 AND \"CompletionAuditId\" IS NOT NULL)) AND " +
        "(\"CompletionAuditId\" IS NULL OR \"CompletionAuditId\" <> \"AdmissionAuditId\")";
    private const string AuthoritySql = "\"AuthenticationGeneration\" > 0 AND \"FactorGeneration\" > 0 AND \"EnrollmentRevision\" >= 0";
    private const string TimeSql = "\"FactorVerifiedAt\" >= 0 AND \"CreatedAt\" >= \"FactorVerifiedAt\" AND \"ObservedAt\" >= \"CreatedAt\" AND " +
        "\"ApprovalUntil\" > \"CreatedAt\" AND \"ApprovalUntil\" - \"FactorVerifiedAt\" <= 300";
    private const string TargetSql = "\"Kind\" IN ('scope', 'audience') AND \"Revision\" > 0 AND \"TargetRevision\" > 0";

    /// <summary>Preserves exact actor/action/target binding, indexed bounded polling and independent admission/completion audit references.</summary>
    internal static void Configure(ModelBuilder builder, IDatabaseModelDialect dialect)
    {
        var row = builder.Entity<CatalogueDeletion>();
        row.HasKey(value => value.Id);
        FactorSchema.Text(row, nameof(CatalogueDeletion.Kind), CatalogueVersion.Audience.Length, dialect);
        FactorSchema.Text(row, nameof(CatalogueDeletion.TargetId), CatalogueRules.IdentifierLength, dialect);
        FactorSchema.Text(row, nameof(CatalogueDeletion.ActorId), FactorSchema.UserLength, dialect);
        FactorSchema.Text(row, nameof(CatalogueDeletion.SecurityStamp), FactorSchema.StampLength, dialect);
        FactorSchema.Text(row, nameof(CatalogueDeletion.ScopedKeyHash), FactorSchema.DigestLength, dialect);
        FactorSchema.Text(row, nameof(CatalogueDeletion.RequestHash), FactorSchema.DigestLength, dialect);
        row.Property(value => value.Revision).IsConcurrencyToken();
        row.Property(value => value.State).HasConversion<int>();
        row.Property(value => value.Failure).HasConversion<int>();
        row.HasIndex(value => value.ScopedKeyHash).IsUnique();
        row.HasIndex(value => new { value.State, value.ObservedAt, value.Id });
        row.HasIndex(value => new { value.Kind, value.TargetId, value.State });
        row.HasOne<SecurityAuditIntent>().WithOne().HasForeignKey<CatalogueDeletion>(value => value.AdmissionAuditId).OnDelete(DeleteBehavior.Restrict);
        row.HasOne<SecurityAuditIntent>().WithOne().HasForeignKey<CatalogueDeletion>(value => value.CompletionAuditId).OnDelete(DeleteBehavior.Restrict);
        row.ToTable(nameof(CatalogueDeletion), table =>
        {
            table.HasCheckConstraint(CatalogueDeletionStateConstraint, dialect.Identifiers(StateSql));
            table.HasCheckConstraint(CatalogueDeletionAuthorityConstraint, dialect.Identifiers(AuthoritySql));
            table.HasCheckConstraint(CatalogueDeletionTimeConstraint, dialect.Identifiers(TimeSql));
            table.HasCheckConstraint(CatalogueDeletionTargetConstraint, dialect.Identifiers(TargetSql));
        });
    }
}
