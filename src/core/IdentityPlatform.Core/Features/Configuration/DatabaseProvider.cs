namespace IdentityPlatform.Core.Features.Configuration;

public enum DatabaseProvider
{
    PostgreSql,
    SqlServer
}

public static class DatabaseProviderParser
{
    public static DatabaseProvider Parse(string? value)
    {
        // Enum.TryParse also accepts numeric values and combined names; configuration must not.
        return value?.Trim() switch
        {
            { } name when name.Equals("PostgreSql", StringComparison.OrdinalIgnoreCase)
                => DatabaseProvider.PostgreSql,
            { } name when name.Equals("SqlServer", StringComparison.OrdinalIgnoreCase)
                => DatabaseProvider.SqlServer,
            _ => throw new ArgumentException("Database:Provider must be PostgreSql or SqlServer.")
        };
    }
}
