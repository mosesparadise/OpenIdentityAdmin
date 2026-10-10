using IdentityPlatform.Core.Features.Configuration;
using IdentityPlatform.Infrastructure.Persistence;
using IdentityPlatform.Infrastructure.Features.Health;
using IdentityPlatform.Migrator.Features.Database.LocalRoles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Host.Tests.Features.Configuration;

public sealed class PersistenceConfigurationTests
{
    [Theory]
    [InlineData(DatabaseProvider.PostgreSql, "")]
    [InlineData(DatabaseProvider.PostgreSql, "synthetic-secret")]
    [InlineData(DatabaseProvider.PostgreSql, "Password=synthetic-secret")]
    [InlineData(DatabaseProvider.PostgreSql, "Host=localhost")]
    [InlineData(DatabaseProvider.SqlServer, "")]
    [InlineData(DatabaseProvider.SqlServer, "synthetic-secret")]
    [InlineData(DatabaseProvider.SqlServer, "Password=synthetic-secret")]
    [InlineData(DatabaseProvider.SqlServer, "Server=localhost")]
    [InlineData((DatabaseProvider)99, "Password=synthetic-secret")]
    public void Invalid_connections_are_rejected_before_build_without_echoing_input(DatabaseProvider provider, string connection)
    {
        var identity = Assert.Throws<ArgumentException>(() => new ServiceCollection().AddIdentityPersistence(provider, connection));
        var sessions = Assert.Throws<ArgumentException>(() => new ServiceCollection().AddSessionPersistence(provider, connection));
        Assert.DoesNotContain("synthetic-secret", identity.ToString());
        Assert.DoesNotContain("synthetic-secret", sessions.ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("shortA1")]
    [InlineData("ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789")]
    [InlineData("abcdefghijklmnopqrstuvwxyz0123456789")]
    [InlineData("abcdefghijklmnopqrstuvwxyzABCDEFGHIJ")]
    [InlineData("aA123456789012345678901234567890';--")]
    public void Invalid_local_passwords_cannot_reach_database_ddl(string password)
    {
        Assert.Throws<ArgumentException>(() => LocalRuntimeRoleProvisioner.ValidatePassword(password));
    }

    [Fact]
    public void Local_password_length_boundaries_are_explicit()
    {
        LocalRuntimeRoleProvisioner.ValidatePassword("aA1" + new string('x', 29));
        LocalRuntimeRoleProvisioner.ValidatePassword("aA1" + new string('x', 125));
        Assert.Throws<ArgumentException>(() => LocalRuntimeRoleProvisioner.ValidatePassword("aA1" + new string('x', 126)));
    }

    [Fact]
    public void Undefined_provider_cannot_produce_a_schema_contract_or_provider_options()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SchemaCatalog.Platform((DatabaseProvider)99));
        Assert.Throws<ArgumentOutOfRangeException>(() => SchemaCatalog.Sessions((DatabaseProvider)99));
        Assert.Throws<ArgumentOutOfRangeException>(() => PersistenceRegistration.ConfigureDatabase(
            new DbContextOptionsBuilder(), (DatabaseProvider)99, "synthetic", "PlatformMigrations"));
    }
}
