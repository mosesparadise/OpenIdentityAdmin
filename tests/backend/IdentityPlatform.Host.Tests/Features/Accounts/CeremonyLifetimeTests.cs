using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Host.Tests.Features.Accounts;

/// <summary>Injects resource failures to prove later lifetime owners and tracked authority are always released.</summary>
public sealed class CeremonyLifetimeTests
{
    private const int ConnectionStage = 2;
    private const string UnusedConnection = "Host=unused;Database=unused";
    private const string SyntheticUser = "lifetime-control";

    /// <summary>Native, transaction or connection disposal failure cannot retain cancellation resources or tracked authority.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Given_DisposalFailure_When_CeremonyReleasesResources_Then_AllLaterOwnersAreReleased(int failingStage)
    {
        await using var database = new PlatformDbContext(new DbContextOptionsBuilder<PlatformDbContext>().UseNpgsql(UnusedConnection).Options);
        database.Add(new AdministratorAuthentication { UserId = SyntheticUser });
        using var deadline = new CancellationTokenSource();
        var native = new FailingResource(failingStage == 0);
        var transaction = new FailingResource(failingStage == 1);
        var connection = new FailingResource(failingStage == ConnectionStage);
        await Assert.ThrowsAsync<InvalidOperationException>(() => CeremonyTransaction.ReleaseAsync(database, native, transaction, connection, deadline).AsTask());
        Assert.True(native.Released && transaction.Released && connection.Released);
        Assert.Empty(database.ChangeTracker.Entries());
        Assert.Throws<ObjectDisposedException>(() => deadline.Cancel());
    }

    /// <summary>Records release before a controlled exception without modeling provider success or rollback behavior.</summary>
    private sealed class FailingResource(bool fail) : IDisposable, IAsyncDisposable
    {
        internal bool Released
        {
            get; private set;
        }
        /// <summary>Signals release and optionally interrupts the caller's cleanup path.</summary>
        public void Dispose()
        {
            Released = true;
            if (fail)
            {
                throw new InvalidOperationException();
            }
        }
        /// <summary>Exercises the same controlled failure through asynchronous ownership.</summary>
        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
