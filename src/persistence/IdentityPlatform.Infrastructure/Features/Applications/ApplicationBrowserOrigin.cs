using System.Collections.Immutable;
using IdentityPlatform.Core.Features.Applications;
using IdentityPlatform.Database;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Infrastructure.Features.Applications;

/// <summary>Stores one independently authorized browser origin in a bounded slot belonging to its managed policy.</summary>
internal sealed class ApplicationBrowserOrigin
{
    private const string SlotConstraint = "CK_ApplicationBrowserOrigin_Slot";
    private static readonly string SlotBounds = $"\"Slot\" >= 0 AND \"Slot\" < {BrowserOriginPolicy.MaximumOrigins}";

    public required string ApplicationId
    {
        get; init;
    }
    public required string Origin
    {
        get; init;
    }
    public int Slot
    {
        get; init;
    }

    /// <summary>Reads the registered set in its original slot order under the operation's existing storage owner.</summary>
    internal static ValueTask<string[]> ReadAsync(ProtocolStorageScope scope, string id, CancellationToken token) =>
        scope.Owner.RunAsync(async current => await scope.Owner.Database.Set<ApplicationBrowserOrigin>()
            .AsNoTracking().Where(row => row.ApplicationId == id).OrderBy(row => row.Slot).Select(row => row.Origin)
            .Take(BrowserOriginPolicy.MaximumOrigins + 1).ToArrayAsync(current), token);

    /// <summary>Freezes the ordered original authorization before a manager save, lifecycle transition or grant operation.</summary>
    internal static async ValueTask<ImmutableArray<string>> SnapshotAsync(ProtocolStorageScope scope, ApplicationPolicy policy, CancellationToken token)
    {
        if (policy.Profile != ApplicationEditorRules.BrowserProfile)
        {
            return default;
        }
        var origins = await ReadAsync(scope, policy.ApplicationId, token);
        return BrowserOriginPolicy.IsValid(origins) ? origins.ToImmutableArray() : throw ProtocolStorageOwner.Unavailable();
    }

    /// <summary>Bounds the per-client set and indexes exact origin membership for preflight queries without enumerating registrations.</summary>
    internal static void Configure(ModelBuilder builder, IDatabaseModelDialect dialect)
    {
        var row = builder.Entity<ApplicationBrowserOrigin>();
        row.HasKey(value => new { value.ApplicationId, value.Slot });
        row.Property(value => value.ApplicationId).HasMaxLength(ApplicationProjection.IdentifierLength);
        row.Property(value => value.Origin).HasMaxLength(BrowserOriginPolicy.MaximumOriginLength);
        row.HasIndex(value => new { value.Origin, value.ApplicationId }).IsUnique();
        row.HasOne<ApplicationPolicy>().WithMany().HasForeignKey(value => value.ApplicationId).OnDelete(DeleteBehavior.Restrict);
        row.ToTable(nameof(ApplicationBrowserOrigin), table => table.HasCheckConstraint(SlotConstraint,
            dialect.Identifiers(SlotBounds)));
        if (dialect.IdentityOrdinalCollation is { } ordinal)
        {
            row.Property(value => value.ApplicationId).UseCollation(ordinal);
            row.Property(value => value.Origin).UseCollation(ordinal);
        }
    }
}
