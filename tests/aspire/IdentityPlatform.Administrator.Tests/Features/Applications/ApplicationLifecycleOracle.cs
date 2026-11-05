using IdentityPlatform.Admin.Api.Features.Applications;
using IdentityPlatform.Core.Features.Applications;

namespace IdentityPlatform.Administrator.Tests.Features.Applications;

/// <summary>Independently verifies the exact lifecycle-only persisted change, output and accepted audit provenance.</summary>
internal static class ApplicationLifecycleOracle
{
    private const int Updated = 200;
    private const string DisableEvent = "ApplicationDisabled";
    private const string EnableEvent = "ApplicationEnabled";
    private const string Actor = "User";
    private const string Target = "Application";
    private const string Accepted = "Accepted";

    /// <summary>Requires unchanged native configuration/credential history, one aggregate advance and no collateral prior audit changes.</summary>
    internal static void Require(ApplicationMutationFixture test, ApplicationUpdateSnapshot before, ApplicationUpdateSnapshot after, bool enabled, Guid correlation)
    {
        Assert.Equal(enabled, after.Policy.Enabled);
        Assert.NotEqual(before.Policy.Enabled, after.Policy.Enabled);
        Assert.Equal(before.Policy.Revision + 1, after.Policy.Revision);
        Assert.Equal(before.NativeDigest, after.NativeDigest);
        Assert.Equal(before.Policy.ApplicationId, after.Policy.ApplicationId);
        Assert.Equal(before.Policy.Profile, after.Policy.Profile);
        Assert.Equal(before.Policy.CredentialRevision, after.Policy.CredentialRevision);
        Assert.Equal(before.Policy.CredentialIssuedAt, after.Policy.CredentialIssuedAt);
        Assert.Equal(before.Policy.CredentialExpiresAt, after.Policy.CredentialExpiresAt);
        Assert.Equal(test.Clock.Now.ToUnixTimeSeconds(), after.Policy.ObservedAt);
        var prior = before.Audits.Select(value => value.Id).ToHashSet();
        Assert.Equal(ApplicationUpdateSnapshot.Hash(before.Audits), ApplicationUpdateSnapshot.Hash(after.Audits.Where(value => prior.Contains(value.Id))));
        Assert.Equal(ApplicationUpdateSnapshot.Hash(before.LifecycleAudits), ApplicationUpdateSnapshot.Hash(after.LifecycleAudits.Where(value => prior.Contains(value.AuditId))));
        var audit = Assert.Single(after.Audits, value => !prior.Contains(value.Id));
        Assert.Equal(enabled ? EnableEvent : DisableEvent, audit.Event);
        Assert.Equal(test.Identity.Subject, audit.ActorId);
        Assert.Equal(Actor, audit.ActorType);
        Assert.Equal(after.Policy.ApplicationId, audit.TargetId);
        Assert.Equal(Target, audit.TargetType);
        Assert.Equal(Accepted, audit.Outcome);
        Assert.Equal(correlation, audit.CorrelationId);
        Assert.Null(audit.RelatedCorrelationId);
        Assert.Equal(test.Clock.Now.ToUnixTimeSeconds(), audit.OccurredAt);
        var reason = Assert.Single(after.LifecycleAudits, row => !prior.Contains(row.AuditId));
        Assert.Equal(audit.Id, reason.AuditId);
        Assert.Equal(1, (int)reason.ReasonCode);
    }

    /// <summary>Checks lifecycle response binding without accepting a credential or a stale version.</summary>
    internal static void Output(ApplicationMutationResult result, ApplicationUpdateSnapshot after)
    {
        Assert.Equal(Updated, result.Status);
        Assert.Null(result.Credential);
        Assert.Equal(after.Policy.Enabled, result.Application!.Enabled);
        Assert.Equal(ApplicationVersion.Create(after.Policy.ApplicationId, after.Policy.Revision), result.Version);
    }
}
