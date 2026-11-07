using IdentityPlatform.Database;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Infrastructure.Features.Identity.Persistence;

/// <summary>Coordinates pilot protocol writers without locking authority or native token rows.</summary>
internal sealed class PilotProtocolGate
{
    internal const string SingletonId = "first-party-v1";
    private const int IdLength = 32;
    public string Id { get; set; } = SingletonId;

    /// <summary>Seeds only coordination state; a fresh installation gains no application or pilot authority.</summary>
    internal static void Configure(ModelBuilder builder, IDatabaseModelDialect dialect)
    {
        var gate = builder.Entity<PilotProtocolGate>();
        gate.Property(row => row.Id).HasMaxLength(IdLength);
        if (dialect.IdentityOrdinalCollation is { } ordinal)
        {
            gate.Property(row => row.Id).UseCollation(ordinal);
        }
        gate.HasData(new PilotProtocolGate());
    }
}
