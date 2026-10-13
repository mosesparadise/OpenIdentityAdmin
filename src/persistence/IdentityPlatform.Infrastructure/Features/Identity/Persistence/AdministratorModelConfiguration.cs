using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Infrastructure.Features.Identity.Persistence;

/// <summary>Maps durable bootstrap ownership, authority versions and account enrollment without destructive cascades.</summary>
/// <example><code>AdministratorModelConfiguration.Configure(builder, sqlServer); // Called by the platform context.</code></example>
public static class AdministratorModelConfiguration
{
    private const int UserNameLength = 256;
    private const int UserIdLength = 450;
    private const int StateLength = 32;
    private const string OrdinalCollation = "Latin1_General_100_BIN2";

    /// <summary>Composes portable checked state while retaining provider-specific ordinal identifier storage.</summary>
    /// <example><code>AdministratorModelConfiguration.Configure(builder, Database.IsSqlServer());</code></example>
    public static void Configure(ModelBuilder builder, bool sqlServer)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ConfigureGovernance(builder, sqlServer);
        ConfigureReceipt(builder, sqlServer);
        ConfigureEnrollment(builder, sqlServer);
    }

    /// <summary>Keeps lock revisions separate from accepted authority changes; existing bootstrap consumption remains durable.</summary>
    private static void ConfigureGovernance(ModelBuilder builder, bool sqlServer)
    {
        const string Table = "AdministratorGovernance";
        const string Singleton = "CK_AdministratorGovernance_Singleton";
        const string Revision = "CK_AdministratorGovernance_Revision";
        const string AuthorityRevision = "CK_AdministratorGovernance_AuthorityRevision";
        const string SqlSingleton = "[Id] = 1", PgSingleton = "\"Id\" = 1";
        const string SqlRevision = "[Revision] >= 0", PgRevision = "\"Revision\" >= 0";
        const string SqlAuthority = "[AuthorityRevision] >= 0", PgAuthority = "\"AuthorityRevision\" >= 0";
        var governance = builder.Entity<AdministratorGovernance>();
        governance.Property(value => value.Id).ValueGeneratedNever();
        governance.ToTable(Table, table =>
        {
            table.HasCheckConstraint(Singleton, sqlServer ? SqlSingleton : PgSingleton);
            table.HasCheckConstraint(Revision, sqlServer ? SqlRevision : PgRevision);
            table.HasCheckConstraint(AuthorityRevision, sqlServer ? SqlAuthority : PgAuthority);
        });
        governance.HasData(new AdministratorGovernance());
    }

    /// <summary>Retains immutable bootstrap evidence independently of native user deletion.</summary>
    private static void ConfigureReceipt(ModelBuilder builder, bool sqlServer)
    {
        const string Table = "AdministratorBootstrapReceipt";
        const string Singleton = "CK_AdministratorBootstrapReceipt_Singleton";
        const string SqlSingleton = "[Id] = 1", PgSingleton = "\"Id\" = 1";
        var receipt = builder.Entity<AdministratorBootstrapReceipt>();
        receipt.Property(value => value.Id).ValueGeneratedNever();
        receipt.Property(value => value.OriginalNormalizedUserName).HasMaxLength(UserNameLength);
        receipt.Property(value => value.UserId).HasMaxLength(UserIdLength);
        receipt.ToTable(Table, table => table.HasCheckConstraint(Singleton, sqlServer ? SqlSingleton : PgSingleton));
        if (sqlServer)
        {
            receipt.Property(value => value.OriginalNormalizedUserName).UseCollation(OrdinalCollation);
            receipt.Property(value => value.UserId).UseCollation(OrdinalCollation);
        }
    }

    /// <summary>Stores exact enrollment state and nonnegative revision without implying credential eligibility.</summary>
    private static void ConfigureEnrollment(ModelBuilder builder, bool sqlServer)
    {
        const string Table = "AdministratorEnrollment";
        const string Revision = "CK_AdministratorEnrollment_Revision";
        const string SqlRevision = "[Revision] >= 0", PgRevision = "\"Revision\" >= 0";
        var enrollment = builder.Entity<AdministratorEnrollment>();
        enrollment.HasKey(value => value.UserId);
        enrollment.Property(value => value.UserId).HasMaxLength(UserIdLength);
        enrollment.Property(value => value.State).HasMaxLength(StateLength);
        enrollment.ToTable(Table, table => table.HasCheckConstraint(Revision, sqlServer ? SqlRevision : PgRevision));
        if (sqlServer)
        {
            enrollment.Property(value => value.UserId).UseCollation(OrdinalCollation);
            enrollment.Property(value => value.State).UseCollation(OrdinalCollation);
        }
    }
}
