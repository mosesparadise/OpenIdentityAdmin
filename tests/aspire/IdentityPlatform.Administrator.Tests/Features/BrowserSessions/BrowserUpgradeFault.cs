using System.Data.Common;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Targets actual upgrade audit/commit boundaries without replacing database execution or claiming ambiguous rollback.</summary>
internal sealed class BrowserUpgradeFault(UpgradeFailure mode, CancellationTokenSource cancellation) : SaveChangesInterceptor, IDbTransactionInterceptor, IDbCommandInterceptor
{
    private const string Update = "UPDATE";
    private const string Tokens = "OpenIddictTokens";
    private const string FailureMessage = "Synthetic browser upgrade failure.";
    private Guid? transaction;
    internal bool Armed
    {
        get; set;
    }
    internal int Faults
    {
        get; private set;
    }
    internal int Attempts
    {
        get; private set;
    }
    internal int Commits
    {
        get; private set;
    }
    internal Exception Failure { get; } = mode == UpgradeFailure.Cancellation ? new OperationCanceledException(cancellation.Token) : new InvalidOperationException(FailureMessage);

    /// <summary>Attaches observers to genuine runtime contexts; initialization runs while the fault is unarmed.</summary>
    internal void Configure(IServiceCollection services) => services.AddDbContext<PlatformDbContext>((_, options) => options.AddInterceptors(this));

    /// <summary>Fails exactly when the real command has staged its upgrade audit after native metadata and request revocation.</summary>
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (Armed && ((mode == UpgradeFailure.Audit && data.Context!.ChangeTracker.Entries<SecurityAuditIntent>().Any(entry => entry.Entity.Event == BrowserUpgradeFixture.AuditEvent)) ||
            (mode == UpgradeFailure.Application && data.Context!.ChangeTracker.Entries<OpenIddictEntityFrameworkCoreApplication>().Any(entry => entry.State == EntityState.Modified))))
        {
            Faults++;
            throw Failure;
        }
        return ValueTask.FromResult(result);
    }

    /// <summary>Fails the actual native request revocation UPDATE after the preceding application metadata update.</summary>
    public ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData data, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (Armed && mode == UpgradeFailure.Revocation && command.CommandText.TrimStart().StartsWith(Update, StringComparison.OrdinalIgnoreCase) && command.CommandText.Contains(Tokens, StringComparison.Ordinal))
        {
            Faults++;
            throw Failure;
        }
        return ValueTask.FromResult(result);
    }

    /// <summary>Injects failure or explicit original-request cancellation only after the actual command reaches provider commit.</summary>
    public ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction value, TransactionEventData data, InterceptionResult result, CancellationToken cancellationToken = default)
    {
        if (Armed)
        {
            Assert.Null(transaction);
            transaction = data.TransactionId;
            Attempts++;
            if (mode is UpgradeFailure.BeforeCommit or UpgradeFailure.Cancellation)
            {
                Faults++;
                if (mode == UpgradeFailure.Cancellation)
                {
                    cancellation.Cancel();
                }
                throw Failure;
            }
        }
        return ValueTask.FromResult(result);
    }

    /// <summary>Loses acknowledgement only after the same real transaction has committed durably.</summary>
    public Task TransactionCommittedAsync(DbTransaction value, TransactionEndEventData data, CancellationToken cancellationToken = default)
    {
        if (Armed)
        {
            Assert.Equal(transaction, data.TransactionId);
            Commits++;
            if (mode == UpgradeFailure.AfterCommit)
            {
                Faults++;
                throw Failure;
            }
        }
        return Task.CompletedTask;
    }

    /// <summary>Rejects an unrelated failure being credited as the reached synthetic transaction fault.</summary>
    internal bool Contains(Exception error) => ReferenceEquals(error, Failure) ||
        (error is AggregateException aggregate ? aggregate.InnerExceptions.Count != 0 && aggregate.InnerExceptions.All(Contains) : error.InnerException is { } inner && Contains(inner));
}

/// <summary>Separates audit rollback, precommit failure, explicit cancellation and postcommit acknowledgement loss.</summary>
public enum UpgradeFailure
{
    Audit, Application, Revocation, BeforeCommit, Cancellation, AfterCommit
}
