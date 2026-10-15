using IdentityPlatform.Database;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Host.Tests.Features.DatabaseProviders;

/// <summary>Proves the public extension boundary and cache isolation independently of the shipped provider classes.</summary>
public sealed class ExternalProviderTests
{
    private const string ProjectId = "ProjectOwnedRelational";
    private const string FirstCollation = "C";
    private const string SecondCollation = "POSIX";
    private const string ForeignConnection = "Server=unused;Database=unused";

    /// <summary>An arbitrary project registration satisfies the same shared configuration assertions without a vendor switch.</summary>
    [Fact]
    public void Given_ProjectOwnedImplementation_When_Registered_Then_PublicContractsConfigureBothStores()
    {
        var external = new ExternalDatabaseProvider(ProjectId);
        var provider = new IdentityPlatform.Database.DatabaseProviders(external).Select(ProjectId);
        Assert.Same(external, provider);
        Assert.Null(provider.LocalProvisioning);
        RelationalProviderConformance.VerifyStoreConfiguration(provider, provider.DesignTimeConnection);
        RelationalProviderConformance.VerifyStoreIsolation(provider, provider.DesignTimeConnection);
    }

    /// <summary>Identical identifiers and drivers cannot share different dialect models across registration instances.</summary>
    [Fact]
    public void Given_SameIdAndDriver_When_DialectsDiffer_Then_RegistrationAndDesignTimeModelsRemainIsolated()
    {
        var first = new ExternalDatabaseProvider(ProjectId, FirstCollation);
        var second = new ExternalDatabaseProvider(ProjectId, SecondCollation);
        using var firstContext = Context(first);
        using var secondContext = Context(second);
        Assert.Equal("C", Collation(firstContext));
        Assert.Equal("POSIX", Collation(secondContext));
        Assert.NotSame(firstContext.Model, secondContext.Model);
        var keys = firstContext.GetService<IModelCacheKeyFactory>();
        Assert.NotEqual(keys.Create(firstContext, false), keys.Create(firstContext, true));
        Assert.NotEqual(keys.Create(firstContext, false), keys.Create(secondContext, false));
        using var repeated = Context(first);
        Assert.Same(firstContext.Model, repeated.Model);
    }

    /// <summary>Mixed EF drivers fail when the selected registration is attached, before context use.</summary>
    [Fact]
    public void Given_ForeignEfConfiguration_When_ProviderIsSelected_Then_MismatchedDriverFailsClosed()
    {
        var provider = new ExternalDatabaseProvider(ProjectId);
        var options = new DbContextOptionsBuilder<PlatformDbContext>().UseSqlServer(ForeignConnection);
        Assert.Throws<InvalidOperationException>(() => options.UseDatabaseProvider(provider, provider.DesignTimeConnection, DatabaseStore.Platform));
        Assert.Throws<InvalidOperationException>(() => new PlatformDbContext(new DbContextOptionsBuilder<PlatformDbContext>().Options));
    }

    /// <summary>A foreign connection supplied after registration is rejected by context construction before any open.</summary>
    [Fact]
    public void Given_ForeignConnectionWithOneEfDriver_When_ContextIsConstructed_Then_RejectsBeforeOpening()
    {
        var provider = new ExternalDatabaseProvider(ProjectId);
        var options = new DbContextOptionsBuilder<PlatformDbContext>();
        options.UseDatabaseProvider(provider, provider.DesignTimeConnection, DatabaseStore.Platform);
        using var foreign = new Microsoft.Data.SqlClient.SqlConnection(ForeignConnection);
        options.UseNpgsql(foreign);
        Assert.Throws<InvalidOperationException>(() => new PlatformDbContext(options.Options));
        Assert.Equal(System.Data.ConnectionState.Closed, foreign.State);
    }

    /// <summary>Invalid identifiers cannot enter a catalog even when the implementation itself is trusted.</summary>
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(" spaced ")]
    public void Given_InvalidRegistrationId_When_CatalogIsCreated_Then_Rejects(string id) =>
        Assert.Throws<ArgumentException>(() => new IdentityPlatform.Database.DatabaseProviders(new ExternalDatabaseProvider(id)));

    /// <summary>Builds a model using the public provider options API without service-provider shortcuts.</summary>
    private static PlatformDbContext Context(ExternalDatabaseProvider provider)
    {
        var options = new DbContextOptionsBuilder<PlatformDbContext>();
        options.UseDatabaseProvider(provider, provider.DesignTimeConnection, DatabaseStore.Platform).UseOpenIddict();
        return new PlatformDbContext(options.Options);
    }

    /// <summary>Reads the relational design-time annotation retained for migration comparison.</summary>
    private static string? Collation(PlatformDbContext context) => context.GetService<IDesignTimeModel>().Model
        .FindEntityType(typeof(OpenIddictEntityFrameworkCoreApplication))!.FindProperty(nameof(OpenIddictEntityFrameworkCoreApplication.ClientId))!.GetCollation();
}
