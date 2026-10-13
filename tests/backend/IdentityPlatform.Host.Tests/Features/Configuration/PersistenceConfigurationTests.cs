using IdentityPlatform.Core.Features.Configuration;
using IdentityPlatform.Infrastructure.Features.Health;
using IdentityPlatform.Infrastructure.Persistence;
using IdentityPlatform.Migrator.Features.Database.LocalRoles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Host.Tests.Features.Configuration;

public sealed class PersistenceConfigurationTests
{
    /// <summary>Verify both persistence registrations reject invalid connections before building services.</summary>
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
    public void Given_InvalidConnection_When_PersistenceIsRegistered_Then_RejectsWithoutDisclosingInput(DatabaseProvider provider, string connection)
    {
        var identity = Assert.Throws<ArgumentException>(() => new ServiceCollection().AddIdentityPersistence(provider, connection));
        var sessions = Assert.Throws<ArgumentException>(() => new ServiceCollection().AddSessionPersistence(provider, connection));
        Assert.DoesNotContain("synthetic-secret", identity.ToString());
        Assert.DoesNotContain("synthetic-secret", sessions.ToString());
    }

    /// <summary>Verify local role passwords must meet all validation constraints before DDL.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("shortA1")]
    [InlineData("ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789")]
    [InlineData("abcdefghijklmnopqrstuvwxyz0123456789")]
    [InlineData("abcdefghijklmnopqrstuvwxyzABCDEFGHIJ")]
    [InlineData("aA123456789012345678901234567890';--")]
    public void Given_InvalidLocalPassword_When_Validated_Then_RejectsBeforeDdl(string password)
    {
        Assert.Throws<ArgumentException>(() => LocalRuntimeRoleProvisioner.ValidatePassword(password));
    }

    /// <summary>Verify independently specified minimum and maximum password lengths and the first oversized value.</summary>
    [Fact]
    public void Given_LocalPasswordLengthBoundaries_When_Validated_Then_AcceptsOnlyAllowedLengths()
    {
        const string requiredCharacterClasses = "aA1";
        const int minimumPaddingLength = 29;
        const int maximumPaddingLength = 125;
        LocalRuntimeRoleProvisioner.ValidatePassword(requiredCharacterClasses + new string('x', minimumPaddingLength));
        LocalRuntimeRoleProvisioner.ValidatePassword(requiredCharacterClasses + new string('x', maximumPaddingLength));
        Assert.Throws<ArgumentException>(() => LocalRuntimeRoleProvisioner.ValidatePassword(requiredCharacterClasses + new string('x', maximumPaddingLength + 1)));
    }

    /// <summary>Verify undefined providers cannot select either schema or configure EF options.</summary>
    [Fact]
    public void Given_UndefinedProvider_When_SchemaOrOptionsAreRequested_Then_Rejects()
    {
        const DatabaseProvider undefinedProvider = (DatabaseProvider)99;
        const string unusedConnection = "synthetic";
        const string unusedMigrations = "PlatformMigrations";
        Assert.Throws<ArgumentOutOfRangeException>(() => SchemaCatalog.Platform(undefinedProvider));
        Assert.Throws<ArgumentOutOfRangeException>(() => SchemaCatalog.Sessions(undefinedProvider));
        Assert.Throws<ArgumentOutOfRangeException>(() => PersistenceRegistration.ConfigureDatabase(
            new DbContextOptionsBuilder(), undefinedProvider, unusedConnection, unusedMigrations));
    }
}
