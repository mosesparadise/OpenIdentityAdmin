using System.Text.Json;
using IdentityPlatform.Core.Features.Applications;
using IdentityPlatform.Infrastructure.Features.ApplicationGrants;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;

namespace IdentityPlatform.Infrastructure.Features.Applications;

/// <summary>Retains approved retirement output and all final verifiers until the original owner consumes authority and commits.</summary>
internal sealed class ApplicationRetirementState
{
    private readonly ApplicationRetirement receipt;
    private readonly ApplicationGrantJob job;
    private readonly string expectedJob;
    private readonly ApplicationWriteState? application;
    private readonly GrantPersistence? grants;
    private readonly ApplicationRetirementPersistence? persistence;
    private readonly GrantTargetObservation? replay;
    private const string GuidFormat = "D";
    private DateTimeOffset ReplayUntil => DateTimeOffset.FromUnixTimeSeconds(receipt.ReplayUntil);

    /// <summary>Owns the newly frozen native policy, original approval/job and retirement/audit expectations through all later saves.</summary>
    internal ApplicationRetirementState(ApplicationRetirement receipt, ApplicationGrantJob job, ApplicationWriteState application,
        GrantPersistence grants, ApplicationRetirementPersistence persistence)
    {
        this.receipt = receipt;
        this.job = job;
        this.application = application;
        this.grants = grants;
        this.persistence = persistence;
        expectedJob = JsonSerializer.Serialize(job);
    }

    /// <summary>Freezes current replay progress while retaining the original retirement's immutable version, time and approval.</summary>
    internal ApplicationRetirementState(GrantTargetObservation target, ApplicationGrantJob job)
    {
        receipt = target.Retirement!.Receipt;
        replay = target;
        this.job = job;
        expectedJob = JsonSerializer.Serialize(job);
    }

    /// <summary>Rechecks original native/history bindings and all newly persisted rows, accepting no unobserved mutation in the same owned epoch.</summary>
    internal async Task VerifyAsync(ProtocolStorageScope scope, CancellationToken token)
    {
        if (replay is not null)
        {
            await replay.VerifyAsync(scope, token);
            var actual = await replay.Retirement!.JobAsync(scope, token);
            if (JsonSerializer.Serialize(job) != expectedJob || JsonSerializer.Serialize(actual) != expectedJob)
            {
                throw ProtocolStorageOwner.Unavailable();
            }
            return;
        }
        await persistence!.VerifyAsync(scope, token);
        await grants!.VerifyAsync(scope, token);
        await application!.VerifyAsync(scope, token);
    }

    /// <summary>Retains the original replay window and rejects rollback or equality at its deadline immediately before commit.</summary>
    internal void RequireCurrent(TimeProvider clock)
    {
        var now = clock.GetUtcNow().ToUnixTimeSeconds();
        if (now < receipt.RetiredAt || now >= receipt.ReplayUntil)
        {
            throw new ApplicationMutationRejected(ApplicationMutationFailure.Duplicate);
        }
    }

    /// <summary>Builds an immutable safe response with application and evolving job versions kept explicitly separate.</summary>
    internal ApplicationRetirementAcceptance View(DateTimeOffset deadline) => new(receipt.ApplicationId,
        ApplicationVersion.Create(receipt.ApplicationId, receipt.RetiredRevision), DateTimeOffset.FromUnixTimeSeconds(receipt.RetiredAt),
        receipt.RequestKey.ToString(GuidFormat), GrantProjection.Create(job, deadline < ReplayUntil ? deadline : ReplayUntil));
}
