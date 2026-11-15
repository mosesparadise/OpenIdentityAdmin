using IdentityPlatform.Core.Features.Applications;
using IdentityPlatform.Database;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Infrastructure.Features.Applications;

/// <summary>Records immutable source provenance for a new clone event without inheriting any source audit history or credential.</summary>
internal sealed class ApplicationCloneAuditDetail
{
    public Guid AuditId
    {
        get; set;
    }
    public string SourceApplicationId { get; set; } = string.Empty;
    public string SourceVersion { get; set; } = string.Empty;
}

/// <summary>Bounds clone provenance identically on both providers and retains established audit ownership.</summary>
internal static class ApplicationCloneAuditModel
{
    private const string Table = "ApplicationCloneAuditDetail";
    private const int MaximumVersionLength = 86;

    /// <summary>Links one source observation to its accepted destination audit; no FK enables deleting source identity or history.</summary>
    internal static void Configure(ModelBuilder builder, IDatabaseModelDialect dialect)
    {
        var row = builder.Entity<ApplicationCloneAuditDetail>();
        row.HasKey(value => value.AuditId);
        row.Property(value => value.SourceApplicationId).HasMaxLength(ApplicationProjection.IdentifierLength);
        row.Property(value => value.SourceVersion).HasMaxLength(MaximumVersionLength);
        row.HasOne<SecurityAuditIntent>().WithOne().HasForeignKey<ApplicationCloneAuditDetail>(value => value.AuditId).OnDelete(DeleteBehavior.Cascade);
        row.ToTable(Table);
        if (dialect.IdentityOrdinalCollation is { } ordinal)
        {
            row.Property(value => value.SourceApplicationId).UseCollation(ordinal);
            row.Property(value => value.SourceVersion).UseCollation(ordinal);
        }
    }
}
