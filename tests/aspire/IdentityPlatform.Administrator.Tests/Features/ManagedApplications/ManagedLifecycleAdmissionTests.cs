using System.Net;
using IdentityPlatform.Administrator.Tests.Features.Applications;
using IdentityPlatform.Administrator.Tests.Features.Factors;
using IdentityPlatform.Administrator.Tests.Fixtures;

namespace IdentityPlatform.Administrator.Tests.Features.ManagedApplications;

/// <summary>Isolates actual disabled-client admission after a genuine authorized lifecycle write, before any managed issuer request exists.</summary>
[Collection("administrator-bootstrap")]
public sealed class ManagedLifecycleAdmissionTests(BootstrapFixture fixture)
{
    private const string Bypass = "application-lifecycle-enforcement-bypass";
    private const string DisableEvent = "ApplicationDisabled";
    private const string Actor = "User";
    private const string Target = "Application";
    private const string Accepted = "Accepted";

    /// <summary>The first real issuer request rejects a persisted disabled registration without creating native, human or administrator authority.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests)), Trait("CriticalDecision", "application-lifecycle-enforcement")]
    public async Task Given_DisabledRegistration_When_FirstIssuerRequestArrives_Then_AdmissionIsUnavailableWithoutStateChanges(string provider)
    {
        await using var test = await ManagedCacheTestContext.CreateAsync(fixture, provider);
        var before = await ApplicationHttpUpdateEvidence.SnapshotAsync(test.Http, test.Id);
        await ManagedLifecycleJourneyTests.TransitionAsync(test, false, before.Policy.Revision);
        var disabled = await ApplicationHttpUpdateEvidence.SnapshotAsync(test.Http, test.Id);
        RequireDisabled(test, before, disabled);
        var authority = await ManagedJourneyEvidence.DigestAsync(test);
        using var response = await test.SendAsync(ManagedCacheTestContext.Initial());
        Assert.False(response.StatusCode == HttpStatusCode.Redirect, Bypass);
        await ManagedCacheTestContext.UnavailableAsync(response);
        Assert.Equal(disabled.Digest, (await ApplicationHttpUpdateEvidence.SnapshotAsync(test.Http, test.Id)).Digest);
        Assert.Equal(authority, await ManagedJourneyEvidence.DigestAsync(test));
    }

    /// <summary>Checks committed lifecycle, native credentials, original deadlines and exactly one attributed finite-reason audit independently.</summary>
    private static void RequireDisabled(ManagedCacheTestContext test, ApplicationUpdateSnapshot before, ApplicationUpdateSnapshot after)
    {
        Assert.True(before.Policy.Enabled);
        Assert.False(after.Policy.Enabled);
        Assert.Equal(before.Policy.Revision + 1, after.Policy.Revision);
        Assert.Equal(before.NativeDigest, after.NativeDigest);
        Assert.Equal(before.Policy.ApplicationId, after.Policy.ApplicationId);
        Assert.Equal(before.Policy.Profile, after.Policy.Profile);
        Assert.Equal(before.Policy.CredentialRevision, after.Policy.CredentialRevision);
        Assert.Equal(before.Policy.CredentialIssuedAt, after.Policy.CredentialIssuedAt);
        Assert.Equal(before.Policy.CredentialExpiresAt, after.Policy.CredentialExpiresAt);
        Assert.Equal(test.Http.Now.ToUnixTimeSeconds(), after.Policy.ObservedAt);
        var prior = before.Audits.Select(row => row.Id).ToHashSet();
        Assert.Equal(ApplicationUpdateSnapshot.Hash(before.Audits), ApplicationUpdateSnapshot.Hash(after.Audits.Where(row => prior.Contains(row.Id))));
        Assert.Equal(ApplicationUpdateSnapshot.Hash(before.LifecycleAudits), ApplicationUpdateSnapshot.Hash(after.LifecycleAudits.Where(row => prior.Contains(row.AuditId))));
        var audit = Assert.Single(after.Audits, row => !prior.Contains(row.Id));
        Assert.Equal(DisableEvent, audit.Event);
        Assert.Equal(test.Http.Environment.Issuer.Account.ActorId, audit.ActorId);
        Assert.Equal(Actor, audit.ActorType);
        Assert.Equal(test.Id, audit.TargetId);
        Assert.Equal(Target, audit.TargetType);
        Assert.Equal(Accepted, audit.Outcome);
        Assert.NotEqual(Guid.Empty, audit.CorrelationId);
        Assert.Null(audit.RelatedCorrelationId);
        Assert.Equal(test.Http.Now.ToUnixTimeSeconds(), audit.OccurredAt);
        var reason = Assert.Single(after.LifecycleAudits, row => !prior.Contains(row.AuditId));
        Assert.Equal(audit.Id, reason.AuditId);
        Assert.Equal(1, (int)reason.ReasonCode);
    }
}
