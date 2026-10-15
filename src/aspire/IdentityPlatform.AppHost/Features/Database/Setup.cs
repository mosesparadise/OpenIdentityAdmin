using IdentityPlatform.Database.PostgreSql;
using IdentityPlatform.Database.Hosting;
using IdentityPlatform.Database.PostgreSql.Hosting;
using IdentityPlatform.Database.SqlServer.Hosting;

namespace IdentityPlatform.AppHost.Features.Database;

internal static class Setup
{
    private const string ProviderSetting = "Database:Provider";
    private const string PlatformPasswordName = "platform-runtime-password";
    private const string SessionsPasswordName = "sessions-runtime-password";
    private const int PasswordLength = 40;
    private const int MinimumCharacterClass = 2;

    private static readonly DatabaseHostingProviders Providers = new(new PostgreSqlDatabaseHostingProvider(), new SqlServerDatabaseHostingProvider());
    /// <summary>Selects an explicit local adapter and creates nonpersisted passwords before validated resource wiring.</summary>
    internal static DatabaseResources AddDatabase(this IDistributedApplicationBuilder builder)
    {
        var provider = Providers.Select(builder.Configuration[ProviderSetting] ?? PostgreSqlDatabaseProvider.Identifier);

        var platformPassword = builder.AddParameter(PlatformPasswordName, new GenerateParameterDefault
        {
            MinLength = PasswordLength,
            MinLower = MinimumCharacterClass,
            MinUpper = MinimumCharacterClass,
            MinNumeric = MinimumCharacterClass,
            Special = false
        }, secret: true, persist: false);
        var sessionsPassword = builder.AddParameter(SessionsPasswordName, new GenerateParameterDefault
        {
            MinLength = PasswordLength,
            MinLower = MinimumCharacterClass,
            MinUpper = MinimumCharacterClass,
            MinNumeric = MinimumCharacterClass,
            Special = false
        }, secret: true, persist: false);

        return Providers.Add(provider.Provider.Id, builder, platformPassword, sessionsPassword);
    }
}
