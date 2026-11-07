using System.Globalization;
using IdentityPlatform.Core.Features.ApplicationGrants;
using IdentityPlatform.Core.Features.Applications;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;

namespace IdentityPlatform.Infrastructure.Features.ApplicationGrants;

/// <summary>Projects only finite metadata and lossless counters, without retaining native rows or live scoped services.</summary>
internal static class GrantProjection
{
    private const string GuidFormat = "D";
    private const string Queued = "queued";
    private const string Processing = "processing";
    private const string Stopped = "stopped";
    private const string Succeeded = "succeeded";
    private const string None = "none";
    private const string Authority = "authority";
    private const string Target = "target";
    private const string Storage = "storage";
    private const string Clock = "clock";

    /// <summary>Rejects malformed persisted output instead of publishing inconsistent state, negative progress or invalid chronology.</summary>
    internal static GrantJobView Create(ApplicationGrantJob job, DateTimeOffset deadline)
    {
        if (job.Id == Guid.Empty || job.EffectiveApprovalId == Guid.Empty || !ApplicationEditorRules.IsText(job.ApplicationId, ApplicationProjection.IdentifierLength) ||
            job.Revision <= 0 || job.AuthorizationCount < 0 || job.TokenCount < 0 || job.CreatedAt < 0 || job.ObservedAt < job.CreatedAt ||
            (job.State == GrantJobState.Stopped) != (job.Failure != GrantJobFailure.None))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        return new(job.Id.ToString(GuidFormat), job.ApplicationId, ApplicationVersion.Create(job.Id.ToString(GuidFormat), job.Revision), State(job.State), Failure(job.Failure),
            job.AuthorizationCount.ToString(CultureInfo.InvariantCulture), job.TokenCount.ToString(CultureInfo.InvariantCulture),
            DateTimeOffset.FromUnixTimeSeconds(job.CreatedAt), DateTimeOffset.FromUnixTimeSeconds(job.ObservedAt), deadline);
    }

    /// <summary>Maps only the closed durable state set, rejecting unknown database values.</summary>
    private static string State(GrantJobState state) => state switch
    {
        GrantJobState.Queued => Queued,
        GrantJobState.Processing => Processing,
        GrantJobState.Stopped => Stopped,
        GrantJobState.Succeeded => Succeeded,
        _ => throw ProtocolStorageOwner.Unavailable()
    };

    /// <summary>Publishes only a safe finite failure category without account details or native exceptions.</summary>
    private static string Failure(GrantJobFailure failure) => failure switch
    {
        GrantJobFailure.None => None,
        GrantJobFailure.Authority => Authority,
        GrantJobFailure.Target => Target,
        GrantJobFailure.Storage => Storage,
        GrantJobFailure.Clock => Clock,
        _ => throw ProtocolStorageOwner.Unavailable()
    };
}
