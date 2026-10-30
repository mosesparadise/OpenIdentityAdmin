using IdentityPlatform.Administrator.Tests.Features.BrowserSessions;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Identity.ManagedApplications;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Administrator.Tests.Features.ManagedApplications;

/// <summary>Proves real fresh password/MFA and explicit managed consent without administrator session or native code/token issuance.</summary>
[Collection("administrator-bootstrap")]
public sealed class ManagedHumanHttpTests(BootstrapFixture fixture)
{
    private const string ExpectedPurpose = "managed-application-login";
    private const string Consumed = "Consumed";

    /// <summary>Existing issuer authentication cannot bypass any managed human phase; explicit approval or cancellation completes only its exact interaction.</summary>
    [Theory]
    [InlineData("PostgreSql", true, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", true, Traits = ["Provider", "SqlServer"])]
    [InlineData("PostgreSql", false, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", false, Traits = ["Provider", "SqlServer"])]
    public async Task Given_RealManagedCacheAndExistingIssuerCookie_When_FreshPasswordMfaAndExplicitDecisionRun_Then_OnlyManagedConsentCommits(string provider, bool approve)
    {
        await using var test = await ManagedCacheTestContext.CreateAsync(fixture, provider);
        var authority = await ManagedHumanEvidence.AuthorityAsync(test);
        var sessions = (await test.SnapshotAsync()).Sessions;
        var form = await ManagedHumanHttp.StartAsync(test);
        var original = Assert.Single((await test.SnapshotAsync()).Interactions);
        Assert.Equal(ManagedLoginState.PendingPassword, original.State);
        Assert.Null(original.CeremonyId);
        using var password = await ManagedHumanHttp.PasswordAsync(test, form);
        await ManagedHumanHttp.EntryAsync(password);
        var mfa = await ManagedHumanHttp.FormAsync(test.Http.IssuerClient, password.Headers.Location!);
        var afterPassword = Assert.Single((await test.SnapshotAsync()).Interactions);
        Assert.Equal(ManagedLoginState.PendingMfa, afterPassword.State);
        Assert.Equal(test.Http.Now.ToUnixTimeSeconds(), afterPassword.PasswordVerifiedAt);
        Assert.Null(afterPassword.MfaVerifiedAt);
        using var factor = await ManagedHumanHttp.MfaAsync(test, mfa);
        await ManagedHumanHttp.EntryAsync(factor);
        var consent = await ManagedHumanHttp.FormAsync(test.Http.IssuerClient, factor.Headers.Location!);
        var afterMfa = Assert.Single((await test.SnapshotAsync()).Interactions);
        Assert.Equal(ManagedLoginState.PendingConsent, afterMfa.State);
        Assert.Equal(test.Http.Now.ToUnixTimeSeconds(), afterMfa.MfaVerifiedAt);
        Assert.Null(afterMfa.ConsentVerifiedAt);
        using var decision = await BrowserIssuerHttp.PostAsync(test.Http.IssuerClient, ManagedHumanHttp.Path,
            new Dictionary<string, string>(consent, StringComparer.Ordinal) { [ManagedHumanHttp.Decision] = approve ? ManagedHumanHttp.Approve : ManagedHumanHttp.Deny });
        await ManagedHumanHttp.EntryAsync(decision);
        await ManagedHumanHttp.TerminalAsync(test, decision.Headers.Location!, approve);
        await RequireFinalAsync(test, original, sessions, approve);
        Assert.Equal(authority, await ManagedHumanEvidence.AuthorityAsync(test));
        using var replay = await BrowserIssuerHttp.PostAsync(test.Http.IssuerClient, ManagedHumanHttp.Path,
            new Dictionary<string, string>(consent, StringComparer.Ordinal) { [ManagedHumanHttp.Decision] = ManagedHumanHttp.Approve });
        await ManagedCacheTestContext.UnavailableAsync(replay);
    }

    /// <summary>Requires exact native and human deltas while preserving original deadlines and administrator-session cardinality.</summary>
    private static async Task RequireFinalAsync(ManagedCacheTestContext test, ManagedLoginInteraction original, int sessions, bool approve)
    {
        const int ExpectedTransitions = 3;
        var final = await test.SnapshotAsync();
        var row = Assert.Single(final.Interactions);
        Assert.Equal(approve ? ManagedLoginState.Approved : ManagedLoginState.Denied, row.State);
        Assert.Equal(original.ExpiresAt, row.ExpiresAt);
        Assert.Equal(test.Http.Now.ToUnixTimeSeconds(), row.ConsentVerifiedAt);
        Assert.Equal(original.RequestId, row.RequestId);
        Assert.Equal(original.Revision + ExpectedTransitions, row.Revision);
        Assert.Equal(sessions, final.Sessions);
        Assert.Equal(0, final.Authorizations);
        Assert.Equal(OpenIddictConstants.Statuses.Valid, Assert.Single(final.Tokens).Status);
        await RequireProofAsync(test, row);
    }

    /// <summary>Reads the committed exact managed ceremony and current account bindings independently of the HTTP coordinator.</summary>
    private static async Task RequireProofAsync(ManagedCacheTestContext test, ManagedLoginInteraction row)
    {
        await using var scope = test.Http.Scope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var ceremony = await database.Set<AdministratorInteraction>().AsNoTracking().SingleAsync(value => value.Id == row.CeremonyId, TestContext.Current.CancellationToken);
        var user = await database.Users.AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
        var authentication = await database.Set<AdministratorAuthentication>().AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
        var enrollment = await database.Set<AdministratorEnrollment>().AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
        Assert.True(row.UserId == user.Id && row.SecurityStamp == user.SecurityStamp);
        Assert.Equal(authentication.Generation, row.AuthenticationGeneration);
        Assert.Equal(enrollment.FactorGeneration, row.FactorGeneration);
        Assert.Equal(enrollment.Revision, row.EnrollmentRevision);
        Assert.Equal(ExpectedPurpose, ceremony.Purpose);
        Assert.Equal(Consumed, ceremony.State);
        Assert.Null(ceremony.SessionPublicId);
        Assert.Null(ceremony.BrowserIntentDigest);
        Assert.Equal(row.UserId, ceremony.UserId);
        Assert.Equal(row.SecurityStamp, ceremony.SecurityStamp);
        Assert.Equal(row.AuthenticationGeneration, ceremony.Generation);
        Assert.Equal(row.FactorGeneration, ceremony.DestinationGeneration);
        Assert.Equal(row.EnrollmentRevision, ceremony.ExpectedEnrollmentRevision);
        Assert.Equal(row.PasswordVerifiedAt, ceremony.PasswordVerifiedAt);
        Assert.Equal(row.ExpiresAt, ceremony.ExpiresAt);
        Assert.True(row.PasswordVerifiedAt <= row.MfaVerifiedAt && row.MfaVerifiedAt <= row.ConsentVerifiedAt && row.ConsentVerifiedAt < row.ExpiresAt);
    }
}
