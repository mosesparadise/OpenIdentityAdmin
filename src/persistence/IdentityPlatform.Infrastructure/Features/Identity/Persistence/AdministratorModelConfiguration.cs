using IdentityPlatform.Database;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Infrastructure.Features.Identity.Persistence;

/// <summary>Maps durable bootstrap ownership, authority versions and account enrollment without destructive cascades.</summary>
/// <example><code>AdministratorModelConfiguration.Configure(builder, dialect); // Called by the platform context.</code></example>
public static class AdministratorModelConfiguration
{
    private const int UserNameLength = 256;
    private const int UserIdLength = 450;
    private const int StateLength = 32;

    /// <summary>Composes portable checked state while retaining provider-specific ordinal identifier storage.</summary>
    /// <example><code>AdministratorModelConfiguration.Configure(builder, provider.Model);</code></example>
    public static void Configure(ModelBuilder builder, IDatabaseModelDialect dialect)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ConfigureGovernance(builder, dialect);
        ConfigureReceipt(builder, dialect);
        ConfigureEnrollment(builder, dialect);
    }

    /// <summary>Keeps lock revisions separate from accepted authority changes; existing bootstrap consumption remains durable.</summary>
    private static void ConfigureGovernance(ModelBuilder builder, IDatabaseModelDialect dialect)
    {
        const string Table = "AdministratorGovernance";
        const string Singleton = "CK_AdministratorGovernance_Singleton";
        const string Revision = "CK_AdministratorGovernance_Revision";
        const string AuthorityRevision = "CK_AdministratorGovernance_AuthorityRevision";
        const string PgSingleton = "\"Id\" = 1";
        const string PgRevision = "\"Revision\" >= 0";
        const string PgAuthority = "\"AuthorityRevision\" >= 0";
        var governance = builder.Entity<AdministratorGovernance>();
        governance.Property(value => value.Id).ValueGeneratedNever();
        governance.ToTable(Table, table =>
        {
            table.HasCheckConstraint(Singleton, dialect.Identifiers(PgSingleton));
            table.HasCheckConstraint(Revision, dialect.Identifiers(PgRevision));
            table.HasCheckConstraint(AuthorityRevision, dialect.Identifiers(PgAuthority));
        });
        governance.HasData(new AdministratorGovernance());
    }

    /// <summary>Retains immutable bootstrap evidence independently of native user deletion.</summary>
    private static void ConfigureReceipt(ModelBuilder builder, IDatabaseModelDialect dialect)
    {
        const string Table = "AdministratorBootstrapReceipt";
        const string Singleton = "CK_AdministratorBootstrapReceipt_Singleton";
        const string PgSingleton = "\"Id\" = 1";
        var receipt = builder.Entity<AdministratorBootstrapReceipt>();
        receipt.Property(value => value.Id).ValueGeneratedNever();
        receipt.Property(value => value.OriginalNormalizedUserName).HasMaxLength(UserNameLength);
        receipt.Property(value => value.UserId).HasMaxLength(UserIdLength);
        receipt.ToTable(Table, table => table.HasCheckConstraint(Singleton, dialect.Identifiers(PgSingleton)));
        if (dialect.IdentityOrdinalCollation is { } ordinal)
        {
            receipt.Property(value => value.OriginalNormalizedUserName).UseCollation(ordinal);
            receipt.Property(value => value.UserId).UseCollation(ordinal);
        }
    }

    /// <summary>Stores exact enrollment state and nonnegative revision without implying credential eligibility.</summary>
    private static void ConfigureEnrollment(ModelBuilder builder, IDatabaseModelDialect dialect)
    {
        const string Table = "AdministratorEnrollment";
        const string Revision = "CK_AdministratorEnrollment_Revision";
        const string PgRevision = "\"Revision\" >= 0";
        var enrollment = builder.Entity<AdministratorEnrollment>();
        enrollment.HasKey(value => value.UserId);
        enrollment.Property(value => value.UserId).HasMaxLength(UserIdLength);
        enrollment.Property(value => value.State).HasMaxLength(StateLength);
        enrollment.ToTable(Table, table => table.HasCheckConstraint(Revision, dialect.Identifiers(PgRevision)));
        if (dialect.IdentityOrdinalCollation is { } ordinal)
        {
            enrollment.Property(value => value.UserId).UseCollation(ordinal);
            enrollment.Property(value => value.State).UseCollation(ordinal);
        }
    }
}
