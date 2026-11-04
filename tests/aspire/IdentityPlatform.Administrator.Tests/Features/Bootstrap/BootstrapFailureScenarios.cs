using IdentityPlatform.Administrator.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using static IdentityPlatform.Administrator.Tests.Features.Bootstrap.BootstrapCancellation;
using static IdentityPlatform.Administrator.Tests.Features.Bootstrap.BootstrapEvidence;

namespace IdentityPlatform.Administrator.Tests.Features.Bootstrap;

/// <summary>Failure scenario execution with original lock, cancellation and cleanup ownership.</summary>
internal static class BootstrapFailureScenarios
{
    private const string AuditScenario = "audit";
    private const string ManagerScenario = "manager";
    private const string LockCancellationScenario = "cancel-lock";
    private const string ManagerCancellationScenario = "cancel-manager";
    private const string PrecommitTerminationScenario = "kill-precommit";
    private const string PostgresPermissionDenied = "42501";
    private const int SqlPermissionDenied = 229;
    private const string MissingAuditPermissionDenial = "Expected audit INSERT permission denial.";
    private const int AuditRestoreSeconds = 15;
    private const string MissingGovernanceContention = "Operation was not blocked at the held governance row.";
    private const string SqlServerProvider = "SqlServer";
    private const int ManagerObservationSeconds = 20;
    private const string MissingPendingManagerInsert = "Native manager INSERT did not remain pending.";

    /// <summary>Executes the original single failure branch without retries or a changed cancellation deadline.</summary>
    internal static async Task<string?> RunFailureAsync(BootstrapDatabase lease, string provider, string scenario)
    {
        switch (scenario)
        {
            case AuditScenario:
                await AuditDeniedAsync(lease);
                break;
            case ManagerScenario:
                await ManagerRejectedAsync(lease);
                break;
            case LockCancellationScenario:
                return await CancelLockAsync(lease, provider);
            case ManagerCancellationScenario:
                return await CancelManagerAsync(lease);
            case PrecommitTerminationScenario:
                await KillBeforeCommitAsync(lease, provider);
                break;
        }
        return null;
    }

    /// <summary>Requires the exact provider INSERT denial and restores the grant under the original independent cleanup budget.</summary>
    private static async Task AuditDeniedAsync(BootstrapDatabase lease)
    {
        await lease.AuditPermissionAsync(false, Token);
        try
        {
            var failure = await Record.ExceptionAsync(() => lease.InitializeAsync(Token));
            Assert.True(failure is DbUpdateException && (failure.InnerException is Npgsql.PostgresException { SqlState: PostgresPermissionDenied } ||
                failure.InnerException is Microsoft.Data.SqlClient.SqlException { Number: SqlPermissionDenied }), MissingAuditPermissionDenial);
        }
        finally
        {
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(AuditRestoreSeconds));
            await lease.AuditPermissionAsync(true, cleanup.Token);
        }
    }

    /// <summary>Uses the rejected native manager and requires initialization failure.</summary>
    private static async Task ManagerRejectedAsync(BootstrapDatabase lease)
    {
        await using (var rejected = lease.WithRejectedUsers())
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => lease.InitializeAsync(Token, rejected));
        }
    }

    /// <summary>Observes the held governance row before cancellation, then drains within the original independent budget.</summary>
    private static async Task<string?> CancelLockAsync(BootstrapDatabase lease, string provider)
    {
        string? cancellationFailure = null;
        RequireCancellationClassifierControls();
        using (var cancelled = CancellationTokenSource.CreateLinkedTokenSource(Token))
        await using (var held = await GovernanceLock.AcquireAsync(lease))
        {
            var attempt = lease.InitializeAsync(cancelled.Token);
            try
            {
                await held.ObserveWaitersAsync(lease, 1);
                Assert.False(attempt.IsCompleted, MissingGovernanceContention);
            }
            finally
            {
                cancellationFailure = await CancelAndDrainAsync(attempt, cancelled, sqlAttention: provider == SqlServerProvider);
            }
        }
        return cancellationFailure;
    }

    /// <summary>Observes the native INSERT gate before cancellation, then drains before interceptor disposal.</summary>
    private static async Task<string?> CancelManagerAsync(BootstrapDatabase lease)
    {
        string? cancellationFailure = null;
        var gate = new ManagerCancellationGate();
        await using (var services = lease.WithInterceptor(gate))
        using (var cancelled = CancellationTokenSource.CreateLinkedTokenSource(Token))
        {
            var attempt = lease.InitializeAsync(cancelled.Token, services);
            try
            {
                await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(ManagerObservationSeconds), Token);
                Assert.False(attempt.IsCompleted, MissingPendingManagerInsert);
            }
            finally
            {
                cancellationFailure = await CancelAndDrainAsync(attempt, cancelled);
            }
        }
        return cancellationFailure;
    }

    /// <summary>Terminates the real command while the audit row is held and joins its failed completion.</summary>
    private static async Task KillBeforeCommitAsync(BootstrapDatabase lease, string provider)
    {
        await using (var process = new BootstrapProcess(provider, lease.RuntimeConnection))
        await using (var held = await GovernanceLock.AcquireAsync(lease, audit: true))
        {
            await process.SendAsync(lease.Input());
            await held.ObserveWaitersAsync(lease, 1);
            process.Terminate();
            var result = await process.CompleteAsync();
            Assert.NotEqual(0, result.ExitCode);
        }
    }
}
