using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Infrastructure.Features.Identity.Persistence;

public static class AdministratorModelConfiguration
{
    public static void Configure(ModelBuilder builder, bool sqlServer)
    {
        ArgumentNullException.ThrowIfNull(builder);
        var governance = builder.Entity<AdministratorGovernance>();
        governance.Property(value => value.Id).ValueGeneratedNever();
        governance.ToTable("AdministratorGovernance", table =>
        {
            table.HasCheckConstraint("CK_AdministratorGovernance_Singleton", sqlServer ? "[Id] = 1" : "\"Id\" = 1");
            table.HasCheckConstraint("CK_AdministratorGovernance_Revision", sqlServer ? "[Revision] >= 0" : "\"Revision\" >= 0");
        });
        governance.HasData(new AdministratorGovernance());
        var receipt = builder.Entity<AdministratorBootstrapReceipt>();
        receipt.Property(value => value.Id).ValueGeneratedNever();
        receipt.Property(value => value.OriginalNormalizedUserName).HasMaxLength(256);
        receipt.Property(value => value.UserId).HasMaxLength(450);
        receipt.ToTable("AdministratorBootstrapReceipt", table => table.HasCheckConstraint(
            "CK_AdministratorBootstrapReceipt_Singleton", sqlServer ? "[Id] = 1" : "\"Id\" = 1"));
        var enrollment = builder.Entity<AdministratorEnrollment>();
        enrollment.HasKey(value => value.UserId);
        enrollment.Property(value => value.UserId).HasMaxLength(450);
        enrollment.Property(value => value.State).HasMaxLength(32);
        enrollment.ToTable("AdministratorEnrollment", table => table.HasCheckConstraint(
            "CK_AdministratorEnrollment_Revision", sqlServer ? "[Revision] >= 0" : "\"Revision\" >= 0"));
        // No user cascade: account deletion cannot erase completion evidence or reopen bootstrap.
        if (sqlServer)
        {
            receipt.Property(value => value.OriginalNormalizedUserName).UseCollation("Latin1_General_100_BIN2");
            receipt.Property(value => value.UserId).UseCollation("Latin1_General_100_BIN2");
            enrollment.Property(value => value.UserId).UseCollation("Latin1_General_100_BIN2");
            enrollment.Property(value => value.State).UseCollation("Latin1_General_100_BIN2");
        }
    }
}
