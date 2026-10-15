namespace IdentityPlatform.Database;

/// <summary>Identifies the product-owned logical store; engines cannot redirect one store to the other.</summary>
public enum DatabaseStore
{
    Platform,
    Sessions
}

/// <summary>Defines fixed logical schemas and history names independently of engine syntax.</summary>
/// <example><code>var schema = DatabaseStores.Schema(DatabaseStore.Platform);</code></example>
public static class DatabaseStores
{
    private const string PlatformSchema = "platform";
    private const string SessionsSchema = "bff";
    private const string PlatformHistory = "PlatformMigrations";
    private const string SessionsHistory = "SessionMigrations";

    /// <summary>Returns the logical schema or rejects an undefined store.</summary>
    /// <example><code>DatabaseStores.Schema(DatabaseStore.Sessions); // bff</code></example>
    public static string Schema(DatabaseStore store) => store switch
    {
        DatabaseStore.Platform => PlatformSchema,
        DatabaseStore.Sessions => SessionsSchema,
        _ => throw new ArgumentOutOfRangeException(nameof(store))
    };

    /// <summary>Returns the stable migration history name.</summary>
    /// <example><code>DatabaseStores.History(DatabaseStore.Platform); // PlatformMigrations</code></example>
    public static string History(DatabaseStore store) => store switch
    {
        DatabaseStore.Platform => PlatformHistory,
        DatabaseStore.Sessions => SessionsHistory,
        _ => throw new ArgumentOutOfRangeException(nameof(store))
    };

    /// <summary>Maps only a known history name to its logical store.</summary>
    /// <example><code>var store = DatabaseStores.FromHistory("SessionMigrations");</code></example>
    public static DatabaseStore FromHistory(string history) => history switch
    {
        PlatformHistory => DatabaseStore.Platform,
        SessionsHistory => DatabaseStore.Sessions,
        _ => throw new ArgumentOutOfRangeException(nameof(history))
    };
}
