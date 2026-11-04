using IdentityPlatform.Core.Features.Applications;
using IdentityPlatform.Database;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Infrastructure.Features.Applications;

/// <summary>Adds bounded product metadata without changing native application columns or automatically adopting existing registrations.</summary>
internal static class ApplicationPolicyModel
{
    private const string Table = "ApplicationPolicy";
    private const string RevisionConstraint = "CK_ApplicationPolicy_Revision";
    private const string TimeConstraint = "CK_ApplicationPolicy_Time";
    private const string ProfileConstraint = "CK_ApplicationPolicy_Profile";
    private const string RevisionSql = "\"Revision\" >= \"CredentialRevision\" AND \"CredentialRevision\" > 0";
    private const string TimeSql = "\"CredentialIssuedAt\" >= 0 AND \"CredentialExpiresAt\" > \"CredentialIssuedAt\" AND \"ObservedAt\" >= \"CredentialIssuedAt\"";
    private const string ProfileSql = "\"Profile\" = 'managed-web-v1'";

    /// <summary>Preserves expired credential rows for inspection/replacement and uses portable revision tokens instead of provider-specific row versions.</summary>
    internal static void Configure(ModelBuilder builder, IDatabaseModelDialect dialect)
    {
        var row = builder.Entity<ApplicationPolicy>();
        row.HasKey(value => value.ApplicationId);
        row.Property(value => value.ApplicationId).HasMaxLength(ApplicationProjection.IdentifierLength);
        row.Property(value => value.Profile).HasMaxLength(ApplicationEditorRules.Profile.Length);
        row.Property(value => value.Revision).IsConcurrencyToken();
        row.Property(value => value.CredentialRevision).IsConcurrencyToken();
        row.Property(value => value.ObservedAt).IsConcurrencyToken();
        row.HasIndex(value => value.CredentialExpiresAt);
        row.ToTable(Table, table =>
        {
            table.HasCheckConstraint(RevisionConstraint, dialect.Identifiers(RevisionSql));
            table.HasCheckConstraint(TimeConstraint, dialect.Identifiers(TimeSql));
            table.HasCheckConstraint(ProfileConstraint, dialect.Identifiers(ProfileSql));
        });
        if (dialect.IdentityOrdinalCollation is { } ordinal)
        {
            row.Property(value => value.ApplicationId).UseCollation(ordinal);
            row.Property(value => value.Profile).UseCollation(ordinal);
        }
    }
}
