using IdentityPlatform.Core.Features.ApplicationGrants;
using IdentityPlatform.Infrastructure.Features.Identity.ProtocolAdmission;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Infrastructure.Features.ApplicationGrants;

/// <summary>Owns one bounded native batch under governance/account serialization and distinct current durable-job authority.</summary>
internal static class GrantBatchOperation
{
    /// <summary>Processes at most one finite batch, reconciling failures in fresh ownership after actual original cleanup.</summary>
    internal static async Task<bool> ExecuteAsync(IServiceScopeFactory factory, Guid jobId, TimeProvider clock, CancellationToken token)
    {
        var attempt = new GrantAttempt(jobId);
        var request = new ProtocolStorageRequest(clock, token);
        try
        {
            return await request.RunAsync(factory, (scope, current) => OwnedAsync(scope, attempt, current));
        }
        catch
        {
            await request.DrainAsync();
            token.ThrowIfCancellationRequested();
            return await GrantFailure.RecordAsync(factory, attempt, clock, token);
        }
        finally { await request.DrainAsync(); }
    }

    /// <summary>Rechecks candidate binding after lock acquisition; logout/MFA/replay deadlines are deliberately absent from worker authority.</summary>
    private static async ValueTask<bool> OwnedAsync(ProtocolStorageScope scope, GrantAttempt attempt, CancellationToken token)
    {
        scope.Services.GetRequiredService<ProtocolStorageSelection>().Select(ProtocolStorageMode.Owned);
        await using var transaction = await ProtocolStorageTransaction.OpenAsync(scope.Owner);
        var candidate = await GrantJobAccess.ReadAsync(scope, attempt.JobId, token);
        if (candidate is null || !candidate.Runnable)
        {
            return false;
        }
        attempt.ApprovalId = candidate.Approval.Id;
        var ownership = await ProtocolAdmissionOwnership.AcquireAsync(scope.Owner, candidate.Approval.ActorId, token);
        var pair = await GrantJobAccess.ReadAsync(scope, attempt.JobId, token);
        if (pair is null || !pair.Runnable || pair.Approval.Id != candidate.Approval.Id || pair.Approval.ActorId != candidate.Approval.ActorId)
        {
            return false;
        }
        if (scope.Owner.Clock.GetUtcNow().ToUnixTimeSeconds() < pair.Job.ObservedAt)
        {
            attempt.Failure = GrantJobFailure.Clock;
            throw ProtocolStorageOwner.Unavailable();
        }
        if (!await GrantActor.AllowsAsync(scope, ownership, token))
        {
            attempt.Failure = GrantJobFailure.Authority;
            throw ProtocolStorageOwner.Unavailable();
        }
        var target = await RequireTargetAsync(scope, pair.Job.ApplicationId, attempt, token);
        attempt.Failure = GrantJobFailure.Storage;
        var baseline = new GrantBaseline(pair);
        var count = await GrantNativeBatch.RevokeAsync(scope, pair.Job.ApplicationId, token);
        await baseline.VerifyAsync(scope, token);
        var expected = await GrantProgress.StageAsync(scope, pair, count, GrantJobFailure.None, attempt.AuditId, attempt.Correlation, token);
        await GrantNativeBatch.VerifyAsync(scope, pair.Job.ApplicationId, count, token);
        if (!await GrantActor.AllowsAsync(scope, ownership, token))
        {
            attempt.Failure = GrantJobFailure.Authority;
            throw ProtocolStorageOwner.Unavailable();
        }
        await target.VerifyAsync(scope, token);
        attempt.Failure = GrantJobFailure.Storage;
        await expected.VerifyAsync(scope, token);
        if (scope.Owner.Clock.GetUtcNow().ToUnixTimeSeconds() < pair.Job.ObservedAt)
        {
            attempt.Failure = GrantJobFailure.Clock;
            throw ProtocolStorageOwner.Unavailable();
        }
        attempt.CommitAttempted = true;
        await transaction.CommitAsync(token);
        return true;
    }
    /// <summary>Classifies only actual target rejections; database/owned failures remain storage errors.</summary>
    private static async Task<GrantTargetObservation> RequireTargetAsync(ProtocolStorageScope scope, string id, GrantAttempt attempt, CancellationToken token)
    {
        try
        {
            var target = await GrantTarget.ReadAsync(scope, id, token);
            GrantTarget.RequireDisabled(target.Policy);
            return target;
        }
        catch (IdentityPlatform.Infrastructure.Features.Applications.ApplicationMutationRejected)
        {
            attempt.Failure = GrantJobFailure.Target;
            throw;
        }
    }

}

/// <summary>Retains nonsecret reconciliation identity outside the failed scope; a failed attempt never supplies a reusable authority receipt.</summary>
internal sealed class GrantAttempt(Guid jobId)
{
    internal Guid JobId => jobId;
    internal Guid? ApprovalId
    {
        get; set;
    }
    internal Guid AuditId { get; } = Guid.NewGuid();
    internal Guid Correlation { get; } = Guid.NewGuid();
    internal bool CommitAttempted
    {
        get; set;
    }
    internal GrantJobFailure Failure { get; set; } = GrantJobFailure.Storage;
}
