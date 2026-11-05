using IdentityPlatform.Core.Features.Applications;
using IdentityPlatform.Database;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Infrastructure.Features.Applications;

/// <summary>Retains only a finite reason linked to the owning accepted audit, with no submitted free text or duplicate actor/target state.</summary>
internal sealed class ApplicationLifecycleAuditDetail
{
    public Guid AuditId
    {
        get; set;
    }
    public ApplicationLifecycleReason ReasonCode
    {
        get; set;
    }
}

/// <summary>Constrains finite numeric reasons equally on both providers without string collation or padding aliases.</summary>
internal static class ApplicationLifecycleAuditModel
{
    private const string Table = "ApplicationLifecycleAuditDetail";
    private const string Constraint = "CK_ApplicationLifecycleAuditDetail_Reason";
    private const string ReasonSql = "\"ReasonCode\" IN (1, 2, 3)";

    /// <summary>Shares audit transaction/retention ownership; a future authorized audit deletion also removes its dependent reason.</summary>
    internal static void Configure(ModelBuilder builder, IDatabaseModelDialect dialect)
    {
        var row = builder.Entity<ApplicationLifecycleAuditDetail>();
        row.HasKey(value => value.AuditId);
        row.Property(value => value.ReasonCode).HasConversion<int>();
        row.HasOne<SecurityAuditIntent>().WithOne().HasForeignKey<ApplicationLifecycleAuditDetail>(value => value.AuditId).OnDelete(DeleteBehavior.Cascade);
        row.ToTable(Table, table => table.HasCheckConstraint(Constraint, dialect.Identifiers(ReasonSql)));
    }
}
