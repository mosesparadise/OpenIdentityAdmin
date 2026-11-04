using IdentityPlatform.Admin.Api.Features.Applications;
using IdentityPlatform.Core.Features.Applications;
using IdentityPlatform.Infrastructure.Features.Applications;

namespace IdentityPlatform.Administrator.Tests.Features.Applications;

/// <summary>Checks persisted winner state against independently submitted values, retaining exact old state for all fields outside the action.</summary>
internal static class ApplicationUpdateOracle
{
    private const int Updated = 200;
    private const string Actor = "User";
    private const string Target = "Application";
    private const string Accepted = "Accepted";
    private const string EditEvent = "ApplicationEdited";
    private const string ReplaceEvent = "ApplicationSecretReplaced";

    /// <summary>Requires one aggregate advance, one precisely attributed audit and the native credential/configuration effects of the winning command only.</summary>
    internal static async Task RequireAsync(ApplicationMutationFixture test, ApplicationUpdateSnapshot before, ApplicationMutationCommand command,
        ApplicationMutationResult result, Guid correlation)
    {
        var after = await ApplicationUpdateSnapshot.ReadAsync(test, command.ApplicationId!);
        Assert.Equal(Updated, result.Status);
        Assert.Equal(ApplicationVersion.Create(command.ApplicationId!, before.Policy.Revision + 1), result.Version);
        RequireState(test, before, after, command);
        if (command.Kind == ApplicationMutationKind.Edit)
        {
            Assert.Null(result.Credential);
        }
        else
        {
            Assert.NotNull(result.Credential);
            Assert.True(await test.AcceptsAsync(command.ApplicationId!, result.Credential.ClientSecret));
        }
        RequireAudit(test, before, after, command, correlation);
    }

    /// <summary>Checks the actual requested field/hash/lifecycle effects, both inside the faulted transaction and after a successful commit.</summary>
    internal static void RequireState(ApplicationMutationFixture test, ApplicationUpdateSnapshot before, ApplicationUpdateSnapshot after, ApplicationMutationCommand command)
    {
        Assert.Equal(before.Policy.Revision + 1, after.Policy.Revision);
        Assert.Equal(before.StableNativeDigest, after.StableNativeDigest);
        Assert.NotEqual(before.Stamp, after.Stamp);
        Assert.Equal(before.Policy.ApplicationId, after.Policy.ApplicationId);
        Assert.Equal(before.Policy.Profile, after.Policy.Profile);
        Assert.Equal(test.Clock.Now.ToUnixTimeSeconds(), after.Policy.ObservedAt);
        if (command.Kind == ApplicationMutationKind.Edit)
        {
            Assert.Equal(command.DisplayName, after.Display);
            Assert.Equal(command.Redirects.Order(StringComparer.Ordinal), after.Redirects.Order(StringComparer.Ordinal));
            Assert.Equal(before.SecretDigest, after.SecretDigest);
            Assert.Equal(before.Policy.CredentialRevision, after.Policy.CredentialRevision);
            Assert.Equal(before.Policy.CredentialIssuedAt, after.Policy.CredentialIssuedAt);
            Assert.Equal(before.Policy.CredentialExpiresAt, after.Policy.CredentialExpiresAt);
        }
        else
        {
            Assert.NotEqual(before.SecretDigest, after.SecretDigest);
            Assert.Equal(before.Display, after.Display);
            Assert.Equal(before.Redirects, after.Redirects);
            Assert.Equal(before.Policy.CredentialRevision + 1, after.Policy.CredentialRevision);
            Assert.Equal(test.Clock.Now.ToUnixTimeSeconds(), after.Policy.CredentialIssuedAt);
            Assert.Equal(command.CredentialExpiry!.Value.ToUnixTimeSeconds(), after.Policy.CredentialExpiresAt);
        }
    }

    /// <summary>Preserves every previous audit field and requires the new action's exact identity, original correlation and controlled UTC instant.</summary>
    private static void RequireAudit(ApplicationMutationFixture test, ApplicationUpdateSnapshot before, ApplicationUpdateSnapshot after,
        ApplicationMutationCommand command, Guid correlation)
    {
        var prior = before.Audits.Select(value => value.Id).ToHashSet();
        Assert.Equal(ApplicationUpdateSnapshot.Hash(before.Audits), ApplicationUpdateSnapshot.Hash(after.Audits.Where(value => prior.Contains(value.Id))));
        var audit = Assert.Single(after.Audits, value => !prior.Contains(value.Id));
        Assert.Equal(command.Kind == ApplicationMutationKind.Edit ? EditEvent : ReplaceEvent, audit.Event);
        Assert.Equal(test.Identity.Subject, audit.ActorId);
        Assert.Equal(Actor, audit.ActorType);
        Assert.Equal(command.ApplicationId, audit.TargetId);
        Assert.Equal(Target, audit.TargetType);
        Assert.Equal(Accepted, audit.Outcome);
        Assert.Equal(correlation, audit.CorrelationId);
        Assert.Null(audit.RelatedCorrelationId);
        Assert.Equal(test.Clock.Now.ToUnixTimeSeconds(), audit.OccurredAt);
    }
}
