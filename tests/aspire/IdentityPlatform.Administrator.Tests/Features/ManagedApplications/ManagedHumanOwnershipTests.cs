using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.Identity.ManagedApplications;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.ManagedApplications;

/// <summary>Exercises borrowed native leases inside actual provider ownership without replacing transaction or connection implementations.</summary>
[Collection("administrator-bootstrap")]
public sealed class ManagedHumanOwnershipTests(BootstrapFixture fixture)
{
    /// <summary>Borrowing permits shared primitives only during exact owner execution; generic commits and escaped receipts cannot acquire authority.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_ActualOwnedTransaction_When_ManagedCeremonyBorrowsNativeLease_Then_GenericCommitsAndEscapedUseFail(string provider)
    {
        await using var test = await ManagedCacheTestContext.CreateAsync(fixture, provider);
        var before = await ManagedHumanEvidence.DigestAsync(test);
        await using var reader = test.Http.Scope();
        var user = await reader.ServiceProvider.GetRequiredService<PlatformDbContext>().Users.AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
        var services = test.Http.Environment.IssuerServices;
        var original = new ProtocolStorageRequest(services.GetRequiredService<TimeProvider>(), TestContext.Current.CancellationToken);
        await using var scope = ProtocolStorageScope.Create(services.GetRequiredService<IServiceScopeFactory>(), original);
        await scope.ActivateAsync(original, TestContext.Current.CancellationToken);
        await using var transaction = await ProtocolStorageTransaction.OpenAsync(scope.Owner);
        var locks = await ManagedCeremonyLocks.AcquireAsync(scope, new(true, true, [], [user.Id]), TestContext.Current.CancellationToken);
        var escaped = await scope.Owner.RunAsync(async token =>
        {
            var database = (PlatformDbContext)scope.Owner.Database;
            var native = scope.Services.GetRequiredService<NativeOperation>();
            var actual = database.Database.CurrentTransaction;
            var borrowed = CeremonyTransaction.BorrowManaged(locks, database, native, token);
            borrowed.RequireCeremony();
            borrowed.RequireAccountOperation();
            await Assert.ThrowsAsync<FactorUnavailableException>(() => borrowed.CommitAsync(CeremonyEvent.Password, true, user.Id, test.Http.Now.ToUnixTimeSeconds()));
            await Assert.ThrowsAsync<FactorUnavailableException>(borrowed.CommitMaintenanceAsync);
            Assert.Throws<FactorUnavailableException>(borrowed.RequireGovernance);
            await borrowed.DisposeAsync();
            Assert.Same(actual, database.Database.CurrentTransaction);
            Assert.False(native.Token.CanBeCanceled);
            Assert.Equal(1, await database.Users.CountAsync(token));
            return borrowed;
        }, TestContext.Current.CancellationToken, write: true);
        Assert.Throws<FactorUnavailableException>(escaped.RequireCurrent);
        Assert.Throws<InvalidOperationException>(() => locks.Require((PlatformDbContext)scope.Owner.Database));
        await transaction.DisposeAsync();
        Assert.Equal(before, await ManagedHumanEvidence.DigestAsync(test));
    }
}
