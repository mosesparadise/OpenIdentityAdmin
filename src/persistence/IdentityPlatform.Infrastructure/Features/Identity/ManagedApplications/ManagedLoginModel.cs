using IdentityPlatform.Core.Features.Applications;
using IdentityPlatform.Database;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Infrastructure.Features.Identity.ManagedApplications;

/// <summary>Maps finite managed-interaction snapshots; final native attachment and live authority still require the owning transaction's capability.</summary>
internal static class ManagedLoginModel
{
    private const string Table = "ManagedLoginInteraction";
    private const string RevisionConstraint = "CK_ManagedLoginInteraction_Revision";
    private const string TimeConstraint = "CK_ManagedLoginInteraction_Time";
    private const string NativeConstraint = "CK_ManagedLoginInteraction_Native";
    private const string StateConstraint = "CK_ManagedLoginInteraction_State";
    private const string ProofConstraint = "CK_ManagedLoginInteraction_Proof";
    private const string CodeConstraint = "CK_ManagedLoginInteraction_Code";
    private const string RevisionSql = "\"Revision\" > 0 AND \"ApplicationRevision\" > 0";
    private const string TimeSql = "\"CreatedAt\" >= 0 AND \"ExpiresAt\" > \"CreatedAt\" AND \"ExpiresAt\" - \"CreatedAt\" <= 300 AND " +
        "\"ObservedAt\" >= \"CreatedAt\" AND \"RetainUntil\" >= \"ExpiresAt\"";
    private const string NativeSql = "(\"RequestId\" IS NULL AND \"RequestExpiresAt\" IS NULL AND \"NativeDependencyCreatedAt\" IS NULL AND \"NativeDependencyExpiresAt\" IS NULL) OR " +
        "(\"RequestId\" IS NOT NULL AND \"RequestExpiresAt\" IS NOT NULL AND \"NativeDependencyCreatedAt\" IS NOT NULL AND \"NativeDependencyExpiresAt\" IS NOT NULL AND " +
        "\"RequestExpiresAt\" >= \"ExpiresAt\" AND \"NativeDependencyCreatedAt\" >= \"CreatedAt\" AND \"NativeDependencyExpiresAt\" >= \"RequestExpiresAt\" AND " +
        "\"NativeDependencyExpiresAt\" > \"NativeDependencyCreatedAt\" AND \"RetainUntil\" >= \"NativeDependencyExpiresAt\")";
    private const string StateSql = "\"State\" IN (1, 2, 3, 4, 5, 6, 7)";
    private const string PasswordProof = "\"CeremonyId\" IS NOT NULL AND \"CeremonyBrowserDigest\" IS NOT NULL AND \"BrowserBudgetId\" IS NOT NULL AND " +
        "\"UserId\" IS NOT NULL AND \"SecurityStamp\" IS NOT NULL AND \"AuthenticationGeneration\" IS NOT NULL AND \"AuthenticationGeneration\" > 0 AND " +
        "\"FactorGeneration\" IS NOT NULL AND \"FactorGeneration\" > 0 AND \"EnrollmentRevision\" IS NOT NULL AND \"EnrollmentRevision\" >= 0 AND " +
        "\"PasswordVerifiedAt\" IS NOT NULL AND \"PasswordVerifiedAt\" >= \"CreatedAt\" AND \"PasswordVerifiedAt\" < \"ExpiresAt\"";
    private const string NoProof = "\"CeremonyId\" IS NULL AND \"CeremonyBrowserDigest\" IS NULL AND \"BrowserBudgetId\" IS NULL AND \"UserId\" IS NULL AND " +
        "\"SecurityStamp\" IS NULL AND \"AuthenticationGeneration\" IS NULL AND \"FactorGeneration\" IS NULL AND \"EnrollmentRevision\" IS NULL AND " +
        "\"PasswordVerifiedAt\" IS NULL AND \"MfaVerifiedAt\" IS NULL AND \"ConsentVerifiedAt\" IS NULL";
    private const string MfaProof = "\"MfaVerifiedAt\" IS NOT NULL AND \"MfaVerifiedAt\" >= \"PasswordVerifiedAt\" AND \"MfaVerifiedAt\" < \"ExpiresAt\"";
    private const string ConsentProof = "\"ConsentVerifiedAt\" IS NOT NULL AND \"ConsentVerifiedAt\" >= \"MfaVerifiedAt\" AND \"ConsentVerifiedAt\" < \"ExpiresAt\"";
    private const string ProofSql = "\"State\" IN (6, 7) OR (\"State\" = 1 AND " + NoProof + ") OR (\"RequestId\" IS NOT NULL AND " + PasswordProof +
        " AND ((\"State\" = 2 AND \"MfaVerifiedAt\" IS NULL AND \"ConsentVerifiedAt\" IS NULL) OR (\"State\" = 3 AND " + MfaProof +
        " AND \"ConsentVerifiedAt\" IS NULL) OR (\"State\" IN (4, 5) AND " + MfaProof + " AND " + ConsentProof + ")))";
    private const string CodeSql = "(\"State\" = 5 AND \"AuthorizationId\" IS NOT NULL AND \"CodeId\" IS NOT NULL) OR " +
        "(\"State\" <> 5 AND \"AuthorizationId\" IS NULL AND \"CodeId\" IS NULL)";

    /// <summary>Preserves full native account bounds and independent replay retention without any foreign-key deletion cascade.</summary>
    internal static void Configure(ModelBuilder builder, IDatabaseModelDialect dialect)
    {
        var row = builder.Entity<ManagedLoginInteraction>();
        row.HasKey(value => value.SelectorDigest);
        foreach (var name in new[] { nameof(ManagedLoginInteraction.SelectorDigest), nameof(ManagedLoginInteraction.BrowserDigest),
            nameof(ManagedLoginInteraction.DemandDigest), nameof(ManagedLoginInteraction.CeremonyBrowserDigest), nameof(ManagedLoginInteraction.BrowserBudgetId) })
        {
            FactorSchema.Text(row, name, FactorSchema.DigestLength, dialect);
        }
        foreach (var name in new[] { nameof(ManagedLoginInteraction.ApplicationId), nameof(ManagedLoginInteraction.RequestId),
            nameof(ManagedLoginInteraction.AuthorizationId), nameof(ManagedLoginInteraction.CodeId) })
        {
            FactorSchema.Text(row, name, ApplicationProjection.IdentifierLength, dialect);
        }
        FactorSchema.Text(row, nameof(ManagedLoginInteraction.UserId), FactorSchema.UserLength, dialect);
        FactorSchema.Text(row, nameof(ManagedLoginInteraction.SecurityStamp), FactorSchema.StampLength, dialect);
        row.Property(value => value.Revision).IsConcurrencyToken();
        row.Property(value => value.ObservedAt).IsConcurrencyToken();
        row.HasIndex(value => value.RequestId).IsUnique();
        row.HasIndex(value => value.CeremonyId).IsUnique();
        row.HasIndex(value => value.CodeId).IsUnique();
        row.HasIndex(value => value.AuthorizationId).IsUnique();
        row.HasIndex(value => new { value.BrowserDigest, value.State, value.ExpiresAt });
        row.HasIndex(value => new { value.State, value.ExpiresAt });
        row.HasIndex(value => new { value.ApplicationId, value.ApplicationRevision });
        row.HasIndex(value => value.RetainUntil);
        row.ToTable(Table, table =>
        {
            table.HasCheckConstraint(RevisionConstraint, dialect.Identifiers(RevisionSql));
            table.HasCheckConstraint(TimeConstraint, dialect.Identifiers(TimeSql));
            table.HasCheckConstraint(NativeConstraint, dialect.Identifiers(NativeSql));
            table.HasCheckConstraint(StateConstraint, dialect.Identifiers(StateSql));
            table.HasCheckConstraint(ProofConstraint, dialect.Identifiers(ProofSql));
            table.HasCheckConstraint(CodeConstraint, dialect.Identifiers(CodeSql));
        });
    }
}
