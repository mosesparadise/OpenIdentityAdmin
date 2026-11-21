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
    private const string RetirementConstraint = "CK_ApplicationPolicy_Retirement";
    private const string RetirementSql = "CAST(\"Retired\" AS INTEGER) + CAST(\"Enabled\" AS INTEGER) <= 1";
    private const string Credentialed = "\"Profile\" IN ('managed-web-v1', 'managed-web-key-v1', 'managed-machine-v1', 'managed-machine-key-v1')";
    private const string Public = "\"Profile\" IN ('managed-native-v1', 'managed-browser-v1', 'managed-device-v1')";
    private const string RevisionSql = "\"Revision\" >= 1 AND \"Revision\" >= \"CredentialRevision\" AND ((" +
        Credentialed + " AND \"CredentialRevision\" > 0) OR (" + Public + " AND \"CredentialRevision\" = 0))";
    private const string TimeSql = "\"ObservedAt\" >= 0 AND ((" + Credentialed + " AND \"CredentialIssuedAt\" IS NOT NULL " +
        "AND \"CredentialExpiresAt\" IS NOT NULL AND \"CredentialIssuedAt\" >= 0 AND \"CredentialExpiresAt\" > \"CredentialIssuedAt\" " +
        "AND \"ObservedAt\" >= \"CredentialIssuedAt\") OR (" + Public + " AND \"CredentialIssuedAt\" IS NULL AND \"CredentialExpiresAt\" IS NULL))";
    private const string ProfileSql = """RIGHT("Profile", 1) = '1' AND (""" + Credentialed + " OR " + Public + ")";
    private const int ProfileLength = 32;

    /// <summary>Preserves expired credential rows for inspection/replacement and uses portable revision tokens instead of provider-specific row versions.</summary>
    internal static void Configure(ModelBuilder builder, IDatabaseModelDialect dialect)
    {
        var row = builder.Entity<ApplicationPolicy>();
        row.HasKey(value => value.ApplicationId);
        row.Property(value => value.Enabled).HasDefaultValue(true).IsConcurrencyToken();
        row.Property(value => value.Retired).HasDefaultValue(false).IsConcurrencyToken();
        row.Property(value => value.ApplicationId).HasMaxLength(ApplicationProjection.IdentifierLength);
        row.Property(value => value.Profile).HasMaxLength(ProfileLength);
        row.Property(value => value.Revision).IsConcurrencyToken();
        row.Property(value => value.CredentialRevision).IsConcurrencyToken();
        row.Property(value => value.ObservedAt).IsConcurrencyToken();
        row.HasIndex(value => value.CredentialExpiresAt);
        row.ToTable(Table, table =>
        {
            table.HasCheckConstraint(RevisionConstraint, dialect.Identifiers(RevisionSql));
            table.HasCheckConstraint(TimeConstraint, dialect.Identifiers(TimeSql));
            table.HasCheckConstraint(ProfileConstraint, dialect.Identifiers(ProfileSql));
            table.HasCheckConstraint(RetirementConstraint, dialect.Identifiers(RetirementSql));
        });
        if (dialect.IdentityOrdinalCollation is { } ordinal)
        {
            row.Property(value => value.ApplicationId).UseCollation(ordinal);
            row.Property(value => value.Profile).UseCollation(ordinal);
        }
    }
}
