using IdentityPlatform.Database;
using IdentityPlatform.Infrastructure.Features.Catalogue;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Persistence.Tests.Features.ProviderCompatibility;

/// <summary>Verifies the existing least-privileged runtime can own native catalogue writes without gaining schema/history or cross-store permissions.</summary>
public sealed class CatalogueRuntimeTests
{
    private const string Postgres = "PostgreSql";
    private const string Name = "runtime-catalogue-scope";
    private const string Display = "Runtime catalogue";
    private const string Identity = "identity";
    private const string None = "none";
    private const string Consent = "Read identity";

    /// <summary>Uses unchanged provider conformance and a real owned scope transaction, then observes committed rows through a fresh runtime context.</summary>
    [Theory]
    [InlineData(Postgres, Traits = ["Provider", Postgres])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_LeastPrivilegeRuntime_When_OwnedNativeScopeAndPolicyCommit_Then_OnlyPermittedStorageIsUsable(string provider)
    {
        await using var fixture = await ManagedSchemaFixture.CreateAsync(provider);
        await using var observer = fixture.Scope();
        var database = observer.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var native = NativeProviderFixtures.Create(provider, database.Database.GetConnectionString()!);
        await NativeProviderConformance.VerifyAsync(native, fixture.Token);
        await using var runtime = new ServiceCollection().AddLogging().AddPlatformPersistence(native.Provider,
            native.RuntimeConnection(DatabaseStore.Platform), native.RuntimeConnection(DatabaseStore.Sessions)).BuildServiceProvider(validateScopes: true);
        var request = new ProtocolStorageRequest(TimeProvider.System, fixture.Token);
        string id;
        try
        {
            id = await request.RunAsync(runtime.GetRequiredService<IServiceScopeFactory>(), WriteAsync);
        }
        finally { await request.DrainAsync(); }
        await using var read = runtime.CreateAsyncScope();
        var policy = await read.ServiceProvider.GetRequiredService<PlatformDbContext>().Set<ScopePolicy>().AsNoTracking().SingleAsync(fixture.Token);
        Assert.Equal(id, policy.ScopeId);
        Assert.Equal(Identity, policy.Kind);
        Assert.Equal(None, policy.Disclosure);
        Assert.Equal(1, policy.Revision);
        var job = await read.ServiceProvider.GetRequiredService<PlatformDbContext>().Set<CatalogueDeletion>().AsNoTracking().SingleAsync(fixture.Token);
        Assert.Equal(CatalogueDeletionState.Queued, job.State);
        Assert.Equal(CatalogueSchemaRows.ActorLength, job.ActorId.Length);
        Assert.Equal(CatalogueSchemaRows.ApprovalUntil, job.ApprovalUntil);
    }

    /// <summary>Separates manager calls from owner execution leases and commits their shared context exactly once.</summary>
    private static async ValueTask<string> WriteAsync(ProtocolStorageScope scope, CancellationToken token)
    {
        await using var transaction = await ProtocolStorageTransaction.OpenAsync(scope.Owner);
        var manager = scope.Services.GetRequiredService<IOpenIddictScopeManager>();
        var native = await manager.CreateAsync(new OpenIddictScopeDescriptor { Name = Name, DisplayName = Display }, token);
        var id = (await manager.GetIdAsync(native, token))!;
        await scope.Owner.RunAsync(async current =>
        {
            CatalogueSchemaRows.Add(scope.Owner.Database);
            scope.Owner.Database.Add(new ScopePolicy { ScopeId = id, Kind = Identity, Disclosure = None, ConsentText = Consent });
            await scope.Owner.Database.SaveChangesAsync(current);
        }, token, write: true);
        await transaction.CommitAsync(token);
        return id;
    }
}
