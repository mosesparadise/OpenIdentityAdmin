using IdentityPlatform.Core.Features.Configuration;

namespace IdentityPlatform.Infrastructure.Features.Health;

// Runtime services deliberately do not load executable migration assemblies.
// Architecture tests compare these compatibility contracts to the generated migrations.
public static class SchemaCatalog
{
    public static IReadOnlyList<string> Platform(DatabaseProvider provider) => provider switch
    {
        DatabaseProvider.PostgreSql => ["20261010012841_InitialPlatform", "20261010134307_FirstPartyLogin", "20261011131900_BrowserInteractionBudget"],
        DatabaseProvider.SqlServer => ["20261010012848_InitialPlatform", "20261010134320_FirstPartyLogin", "20261011131926_BrowserInteractionBudget"],
        _ => throw new ArgumentOutOfRangeException(nameof(provider))
    };

    public static IReadOnlyList<string> Sessions(DatabaseProvider provider) => provider switch
    {
        DatabaseProvider.PostgreSql => ["20261010011921_InitialSessions"],
        DatabaseProvider.SqlServer => ["20261010011939_InitialSessions"],
        _ => throw new ArgumentOutOfRangeException(nameof(provider))
    };
}
