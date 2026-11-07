using System.Globalization;
using IdentityPlatform.Infrastructure.Persistence;
using IdentityPlatform.Issuer.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Issuer.Lifecycle.Tests.Features.Distribution;

/// <summary>Owns the independent second fixture lease; it never updates native authority or changes product isolation.</summary>
internal sealed class PilotReplayNativeReadBarrier : IAsyncDisposable
{
    private const int ObserveSeconds = 2;
    private const int CleanupSeconds = 15;
    private const string CleanupFailure = "Synthetic native-read barrier cleanup failed.";
    private readonly AsyncServiceScope scope;
    private readonly PlatformDbContext database;
    private IDbContextTransaction? transaction;
    private CancellationTokenSource? observation;
    internal int Session
    {
        get; private set;
    }

    /// <summary>Owns one privileged fixture scope, separate from the initial barrier and both actual issuer processes.</summary>
    internal PilotReplayNativeReadBarrier(IssuerEnvironment environment)
    {
        scope = environment.Services.CreateAsyncScope();
        database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
    }

    /// <summary>Installs a two-phase trigger and holds its second fixture row before real issuance starts.</summary>
    internal async Task InitializeAsync(string code, CancellationToken token)
    {
        await database.Database.OpenConnectionAsync(token);
        await ExecuteAsync(PilotReplayNativeReadSql.Create, token);
        await using (var target = database.Database.GetDbConnection().CreateCommand())
        {
            target.CommandTimeout = ObserveSeconds;
            target.CommandText = PilotReplayNativeReadSql.Target;
            var parameter = target.CreateParameter();
            parameter.ParameterName = PilotReplayNativeReadSql.CodeParameter;
            parameter.Value = code;
            target.Parameters.Add(parameter);
            Assert.Equal(1, await target.ExecuteNonQueryAsync(token));
        }
        await ExecuteAsync(PilotReplayNativeReadSql.Trigger, token);
        transaction = await database.Database.BeginTransactionAsync(token);
        await using var held = database.Database.GetDbConnection().CreateCommand();
        held.Transaction = transaction.GetDbTransaction();
        held.CommandTimeout = ObserveSeconds;
        held.CommandText = PilotReplayBarrierSql.SqlSession;
        Session = Convert.ToInt32(await held.ExecuteScalarAsync(token), CultureInfo.InvariantCulture);
        held.CommandText = PilotReplayNativeReadSql.Hold;
        Assert.Equal(1, await held.ExecuteNonQueryAsync(token));
    }

    /// <summary>Shares one existing two-second replay-observation budget across phase transition and the peer lock proof.</summary>
    internal CancellationToken BeginObservation(CancellationToken token)
    {
        Assert.Null(observation);
        observation = CancellationTokenSource.CreateLinkedTokenSource(token);
        observation.CancelAfter(TimeSpan.FromSeconds(ObserveSeconds));
        return observation.Token;
    }

    /// <summary>Retains the original transition deadline instead of granting the replay a fresh observer window.</summary>
    internal CancellationToken ObservationToken => observation!.Token;

    /// <summary>Releases only the extra fixture lease without cancelling genuine exchanges.</summary>
    internal async Task ReleaseAsync(CancellationToken token)
    {
        if (transaction is { } held)
        {
            await held.RollbackAsync(token);
            await held.DisposeAsync();
            transaction = null;
        }
    }

    /// <summary>Runs bounded fixture DDL with no native identifiers embedded in command text.</summary>
    private async Task ExecuteAsync(string sql, CancellationToken token)
    {
        await using var command = database.Database.GetDbConnection().CreateCommand();
        command.CommandText = sql;
        command.CommandTimeout = ObserveSeconds;
        await command.ExecuteNonQueryAsync(token);
    }

    /// <summary>Attempts release, fixture removal and scope disposal independently, preserving every cleanup failure.</summary>
    public async ValueTask DisposeAsync()
    {
        using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(CleanupSeconds));
        var errors = new List<Exception>();
        try
        {
            await ReleaseAsync(cleanup.Token);
        }
        catch (Exception error) { errors.Add(error); }
        try
        {
            await ExecuteAsync(PilotReplayNativeReadSql.Drop, cleanup.Token);
        }
        catch (Exception error) { errors.Add(error); }
        try
        {
            await scope.DisposeAsync();
        }
        catch (Exception error) { errors.Add(error); }
        observation?.Dispose();
        if (errors.Count != 0)
        {
            throw new AggregateException(CleanupFailure, errors);
        }
    }
}
