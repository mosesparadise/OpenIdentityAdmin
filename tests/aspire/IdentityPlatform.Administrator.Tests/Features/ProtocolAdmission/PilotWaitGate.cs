using System.Runtime.ExceptionServices;
using IdentityPlatform.Administrator.Tests.Features.BrowserSessions;
using IdentityPlatform.AuthServer.Features.Protocol.PilotReplay;
using IdentityPlatform.Infrastructure.Persistence;
using IdentityPlatform.Persistence.Tests.Features.ProviderCompatibility;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;

/// <summary>Holds the actual migration-owned singleton in a separate fixture transaction and proves the exact native waiter/blocker relationship.</summary>
internal sealed class PilotWaitGate(AsyncServiceScope scope, IDbContextTransaction transaction, int session) : IAsyncDisposable
{
    private const int ObservationSeconds = 2;
    private bool released;

    /// <summary>Uses the same production acquisition statement on an independent context; no gate row is inserted or repaired.</summary>
    internal static async Task<PilotWaitGate> AcquireAsync(BrowserHttpsFixture environment)
    {
        var scope = PilotWaitState.Observer(environment);
        try
        {
            var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            var transaction = await database.Database.BeginTransactionAsync(TestContext.Current.CancellationToken);
            try
            {
                await PilotProtocolTransaction.AcquireAsync(database, TestContext.Current.CancellationToken);
                return new(scope, transaction, NativeLockObservation.Session(database.Database.GetDbConnection()));
            }
            catch { await transaction.DisposeAsync(); throw; }
        }
        catch { await scope.DisposeAsync(); throw; }
    }

    /// <summary>A pending HTTP task is necessary but insufficient: the database must identify that exact competing session as blocked by this holder.</summary>
    internal async Task ObserveAsync(BrowserHttpsFixture environment, string provider, PilotWaitProbe probe, Task pending)
    {
        var token = TestContext.Current.CancellationToken;
        await Task.WhenAny(probe.Session.Task, pending).WaitAsync(TimeSpan.FromSeconds(ObservationSeconds), token);
        Assert.True(probe.Session.Task.IsCompletedSuccessfully);
        var peer = await probe.Session.Task;
        Assert.NotEqual(session, peer);
        await new NativeLockObservation(provider, environment.Issuer.Account.Journey.Lease.AdministratorConnection).WaitAsync(peer, session, token);
        Assert.False(pending.IsCompleted);
    }

    /// <summary>Releases the unchanged fixture row once; production code retains its original request and transaction budgets.</summary>
    internal async Task ReleaseAsync()
    {
        if (!released)
        {
            await transaction.RollbackAsync(TestContext.Current.CancellationToken);
            released = true;
        }
    }

    /// <summary>Always releases and joins actual HTTP work, retaining primary and cleanup failures without response contents.</summary>
    internal async Task CompleteAsync(Task<HttpResponseMessage> pending, ExceptionDispatchInfo? failure, bool canceled = false)
    {
        var errors = new List<Exception>();
        if (failure is not null)
        {
            errors.Add(failure.SourceException);
        }
        try
        {
            await ReleaseAsync();
        }
        catch (Exception error) { errors.Add(error); }
        try
        {
            using var response = await pending;
        }
        catch (OperationCanceledException) when (canceled) { /* The caller already required this exact HTTP task to cancel. */ }
        catch (Exception error)
        {
            if (!errors.Contains(error))
            {
                errors.Add(error);
            }
        }
        if (errors.Count > 1)
        {
            throw new AggregateException(errors);
        }
        if (errors.Count == 1)
        {
            ExceptionDispatchInfo.Capture(errors[0]).Throw();
        }
    }

    /// <summary>Disposes the holder before its provider scope, including setup or observation failure paths.</summary>
    public async ValueTask DisposeAsync()
    {
        try
        {
            await transaction.DisposeAsync();
        }
        finally { await scope.DisposeAsync(); }
    }
}
