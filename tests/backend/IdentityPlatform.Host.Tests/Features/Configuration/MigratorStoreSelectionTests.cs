using IdentityPlatform.Infrastructure.Features.Sessions.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using IdentityPlatform.Migrator.Features.Database;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace IdentityPlatform.Host.Tests.Features.Configuration;

/// <summary>Checks real Migrator store composition without opening any database connection.</summary>
public sealed class MigratorStoreSelectionTests
{
    private const string Operation = "Operation";
    private const string Provider = "Database:Provider";
    private const string Platform = "ConnectionStrings:platform";
    private const string Sessions = "ConnectionStrings:adminsessions";
    private const string InvalidSessions = "invalid-sessions-connection";

    /// <summary>Both initializer operations require only Platform credentials, even when an unusable Sessions setting is present.</summary>
    [Theory]
    [InlineData("upgrade-administrator-browser-registration", "PostgreSql", "Host=localhost;Database=unused")]
    [InlineData("upgrade-administrator-browser-registration", "SqlServer", "Server=localhost;Database=unused;Integrated Security=true;Encrypt=true")]
    [InlineData("initialize-administrator", "PostgreSql", "Host=localhost;Database=unused")]
    [InlineData("initialize-administrator-protocol", "PostgreSql", "Host=localhost;Database=unused")]
    [InlineData("initialize-administrator", "SqlServer", "Server=localhost;Database=unused;Integrated Security=true;Encrypt=true")]
    [InlineData("initialize-administrator-protocol", "SqlServer", "Server=localhost;Database=unused;Integrated Security=true;Encrypt=true")]
    public void Given_ExplicitInitializer_When_OnlyPlatformConnectionIsUsable_Then_SessionsStoreIsNotRequired(string operation, string provider, string connection)
    {
        var builder = Create(operation, provider, connection);
        builder.AddDatabase();
        Assert.Contains(builder.Services, service => service.ServiceType == typeof(PlatformDbContext));
        Assert.DoesNotContain(builder.Services, service => service.ServiceType == typeof(AdminSessionDbContext));
    }

    /// <summary>Schema upgrade still validates both connections rather than inheriting initializer-only storage selection.</summary>
    [Theory]
    [InlineData("PostgreSql", "Host=localhost;Database=unused")]
    [InlineData("SqlServer", "Server=localhost;Database=unused;Integrated Security=true;Encrypt=true")]
    public void Given_Upgrade_When_SessionsConnectionIsInvalid_Then_ConfigurationStillRejects(string provider, string connection)
    {
        const string Upgrade = "upgrade";
        var builder = Create(Upgrade, provider, connection);
        Assert.ThrowsAny<ArgumentException>(() => builder.AddDatabase());
    }

    /// <summary>Supplies bounded synthetic connection syntax without constructing a host or accessing an engine.</summary>
    private static HostApplicationBuilder Create(string operation, string provider, string connection)
    {
        var builder = new HostApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [Operation] = operation,
            [Provider] = provider,
            [Platform] = connection,
            [Sessions] = InvalidSessions
        });
        return builder;
    }
}
