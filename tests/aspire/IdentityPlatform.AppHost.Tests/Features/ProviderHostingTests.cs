using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using IdentityPlatform.Database;
using IdentityPlatform.Database.Hosting;
using IdentityPlatform.Database.PostgreSql.Hosting;
using IdentityPlatform.Database.SqlServer.Hosting;

namespace IdentityPlatform.AppHost.Tests.Features;

/// <summary>Checks provider adapter identity before building or starting any resource graph.</summary>
[Trait("Category", "Harness")]
public sealed class ProviderHostingTests
{
    private const string PlatformPassword = "platform-test-password";
    private const string SessionsPassword = "sessions-test-password";
    private const string Unknown = "unknown-private-setting";

    /// <summary>Both shipped adapters return canonical identity and the fixed resource graph without starting containers.</summary>
    [Theory]
    [InlineData("PostgreSql")]
    [InlineData("SqlServer")]
    public void Given_RegisteredAdapter_When_ResourcesAreCreated_Then_SelectedIdentityAndStoreNamesRemain(string name)
    {
        var catalog = Catalog();
        var builder = DistributedApplication.CreateBuilder();
        var platformPassword = builder.AddParameter(PlatformPassword, secret: true);
        var sessionsPassword = builder.AddParameter(SessionsPassword, secret: true);
        var resources = catalog.Add(name, builder, platformPassword, sessionsPassword);
        Assert.Same(catalog.Select(name).Provider, resources.Registration);
        Assert.Equal(name, resources.Provider);
        Assert.Equal("platform", resources.Platform.Resource.Name);
        Assert.Equal("adminsessions", resources.Sessions.Resource.Name);
        Assert.Same(platformPassword, resources.PlatformPassword);
        Assert.Same(sessionsPassword, resources.SessionsPassword);
        Assert.NotNull(resources.PlatformRuntime);
        Assert.NotNull(resources.SessionsRuntime);
    }

    /// <summary>Unknown, duplicate or unstable registrations cannot choose an adapter for child wiring.</summary>
    [Fact]
    public void Given_InvalidAdapterRegistration_When_Selected_Then_FailsBeforeResourceCreation()
    {
        var adapter = new PostgreSqlDatabaseHostingProvider();
        Assert.Throws<ArgumentException>(() => new DatabaseHostingProviders(adapter, adapter));
        Assert.Throws<ArgumentException>(() => new DatabaseHostingProviders());
        Assert.Throws<ArgumentException>(() => new DatabaseHostingProviders([null!]));
        var error = Assert.Throws<ArgumentException>(() => Catalog().Select(Unknown));
        Assert.DoesNotContain(Unknown, error.ToString());
    }

    /// <summary>A deliberately misbound adapter cannot return another registered instance under the same identifier.</summary>
    [Fact]
    public void Given_MismatchedAdapterResult_When_ResourcesAreCreated_Then_RejectsBeforeChildWiring()
    {
        var adapter = new MisboundAdapter();
        var catalog = new DatabaseHostingProviders(adapter);
        var builder = DistributedApplication.CreateBuilder();
        var platformPassword = builder.AddParameter(PlatformPassword, secret: true);
        var sessionsPassword = builder.AddParameter(SessionsPassword, secret: true);
        Assert.Throws<InvalidOperationException>(() => catalog.Add(adapter.Provider.Id, builder, platformPassword, sessionsPassword));
    }

    /// <summary>Builds the explicit two-adapter fixture without relying on production AppHost defaults.</summary>
    private static DatabaseHostingProviders Catalog() => new(new PostgreSqlDatabaseHostingProvider(), new SqlServerDatabaseHostingProvider());

    /// <summary>Returns real unstarted resources owned by a different provider instance to seed identity validation.</summary>
    private sealed class MisboundAdapter : IDatabaseHostingProvider
    {
        private readonly PostgreSqlDatabaseHostingProvider inner = new();
        public IRelationalDatabaseProvider Provider { get; } = new Database.PostgreSql.PostgreSqlDatabaseProvider();

        /// <summary>Creates the resource result under the deliberately different inner registration.</summary>
        public DatabaseResources Add(IDistributedApplicationBuilder builder, IResourceBuilder<ParameterResource> platformPassword,
            IResourceBuilder<ParameterResource> sessionsPassword) => inner.Add(builder, platformPassword, sessionsPassword);
    }
}
