using IdentityPlatform.Administrator.Tests.Features.Factors;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Identity.BrowserCompletion;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.Identity.Journeys;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Proves continuation authority comes only from newly verified password and unused native MFA.</summary>
[Collection("administrator-bootstrap")]
public sealed class IssuerContinuationJourneyTests(BootstrapFixture fixture)
{
    private const string WrongPassword = "synthetic-wrong-browser-password";
    private const string Recovery = "Recovery";
    private const int NewPasswordSeconds = 1;

    /// <summary>An existing enrollment session cannot replace new password verification, and password alone cannot release a completion.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_ExistingIssuerSession_When_NewPasswordAndNativeFactorComplete_Then_OnlyTheNewBoundSessionGrantsCompletion(string provider)
    {
        await using var scenario = await JourneyScenario.CreateAsync(fixture, provider);
        var previous = await scenario.EnrollAsync();
        scenario.Clock.Now = scenario.Clock.Now.AddSeconds(NewPasswordSeconds);
        var attempt = IssuerContinuationProviderFixture.Create();
        Assert.True(await IssuerContinuationProviderFixture.StartAsync(scenario, attempt));
        var original = await IssuerContinuationProviderFixture.RowAsync(scenario, attempt);
        var wrong = await IssuerContinuationProviderFixture.BeginAsync(scenario, attempt, password: WrongPassword);
        Assert.Null(wrong.Interaction);
        Assert.Null((await IssuerContinuationProviderFixture.RowAsync(scenario, attempt)).CompletionDigest);
        var login = await IssuerContinuationProviderFixture.BeginAsync(scenario, attempt);
        Assert.NotNull(login.Interaction);
        var pending = await IssuerContinuationProviderFixture.RowAsync(scenario, attempt);
        Assert.Equal(IssuerContinuationProviderFixture.Pending, pending.State);
        Assert.Null(pending.CompletionDigest);
        Assert.True(pending.InteractionId == login.Interaction && pending.ExpiresAt <= original.ExpiresAt);
        var code = await IssuerContinuationProviderFixture.UnusedCodeAsync(scenario);
        var started = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var grant = await IssuerContinuationProviderFixture.CompleteAsync(scenario, attempt, login.Interaction, code);
        FactorOracle.RequireWindow(scenario.Seed, code, started, valid: true);
        Assert.NotNull(grant);
        await IssuerContinuationProviderFixture.RequireGrantAsync(scenario, attempt, login.Interaction, previous, grant);
        var committed = await IssuerContinuationProviderFixture.SnapshotAsync(scenario);
        await IssuerContinuationProviderFixture.DenyCompletionAsync(() => IssuerContinuationProviderFixture.CompleteAsync(scenario, attempt, login.Interaction, code));
        Assert.True(committed == await IssuerContinuationProviderFixture.SnapshotAsync(scenario));
    }

    /// <summary>A different browser proof, unsupported purpose or equality at original expiry cannot fall back to an ordinary password interaction.</summary>
    [Theory, MemberData(nameof(BindingCases))]
    public async Task Given_PendingContinuation_When_PasswordBindingIsInvalid_Then_NoInteractionOrCompletionCommits(string provider, BindingFailure failure)
    {
        await using var scenario = await JourneyScenario.CreateAsync(fixture, provider);
        _ = await scenario.EnrollAsync();
        var attempt = IssuerContinuationProviderFixture.Create();
        Assert.True(await IssuerContinuationProviderFixture.StartAsync(scenario, attempt));
        if (failure == BindingFailure.Expired)
        {
            scenario.Clock.Now = DateTimeOffset.FromUnixTimeSeconds((await IssuerContinuationProviderFixture.RowAsync(scenario, attempt)).ExpiresAt);
        }
        var before = await IssuerContinuationProviderFixture.SnapshotAsync(scenario);
        var request = failure == BindingFailure.Browser ? IssuerContinuationRequest.Read(attempt.Intent, IssuerContinuationProviderFixture.Selector())! : attempt.Request;
        JourneyPasswordResult? result = null;
        var error = await Record.ExceptionAsync(async () => result = await IssuerContinuationProviderFixture.BeginAsync(scenario, attempt,
            purpose: failure == BindingFailure.Purpose ? Recovery : IssuerContinuationProviderFixture.Login, request: request));
        Assert.Null(result?.Interaction);
        if (error is not null)
        {
            Assert.IsType<FactorUnavailableException>(error);
        }
        Assert.True(before == await IssuerContinuationProviderFixture.SnapshotAsync(scenario));
    }

    /// <summary>The unchanged completion overload must enforce original continuation binding, including lost persisted markers.</summary>
    [Theory, MemberData(nameof(CompletionCases))]
    public async Task Given_BoundPasswordInteraction_When_CompletionBindingIsLost_Then_OrdinaryCompletionCannotCreateAuthority(string provider, CompletionFailure failure)
    {
        await using var scenario = await JourneyScenario.CreateAsync(fixture, provider);
        _ = await scenario.EnrollAsync();
        var attempt = IssuerContinuationProviderFixture.Create();
        Assert.True(await IssuerContinuationProviderFixture.StartAsync(scenario, attempt));
        var login = await IssuerContinuationProviderFixture.BeginAsync(scenario, attempt);
        Assert.NotNull(login.Interaction);
        await BreakBindingAsync(scenario, attempt, login.Interaction, failure);
        var before = await IssuerContinuationProviderFixture.SnapshotAsync(scenario);
        var code = await IssuerContinuationProviderFixture.UnusedCodeAsync(scenario);
        await IssuerContinuationProviderFixture.DenyCompletionAsync(() => IssuerContinuationProviderFixture.CompleteAsync(scenario, attempt, login.Interaction, code,
            purpose: failure == CompletionFailure.Purpose ? IssuerContinuationProviderFixture.Enrollment : IssuerContinuationProviderFixture.Login,
            correlation: failure == CompletionFailure.Browser ? IssuerContinuationProviderFixture.Selector() : null));
        Assert.True(before == await IssuerContinuationProviderFixture.SnapshotAsync(scenario));
    }

    /// <summary>The genuine enrollment factor remains consumed; a different unused native factor completes the same pending continuation once.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_ConsumedEnrollmentFactor_When_LoginReplaysIt_Then_NoCompletionExistsUntilAnUnusedNativeFactorSucceeds(string provider)
    {
        await using var scenario = await JourneyScenario.CreateAsync(fixture, provider);
        var previous = await scenario.EnrollAsync();
        var accepted = await IssuerContinuationProviderFixture.AcceptedCodesAsync(scenario);
        Assert.Single(accepted);
        var used = accepted[0];
        var attempt = IssuerContinuationProviderFixture.Create();
        Assert.True(await IssuerContinuationProviderFixture.StartAsync(scenario, attempt));
        var login = await IssuerContinuationProviderFixture.BeginAsync(scenario, attempt);
        Assert.NotNull(login.Interaction);
        Assert.Null(await IssuerContinuationProviderFixture.CompleteAsync(scenario, attempt, login.Interaction, used));
        Assert.Null((await IssuerContinuationProviderFixture.RowAsync(scenario, attempt)).CompletionDigest);
        var fresh = await IssuerContinuationProviderFixture.UnusedCodeAsync(scenario);
        var grant = await IssuerContinuationProviderFixture.CompleteAsync(scenario, attempt, login.Interaction, fresh);
        Assert.NotNull(grant);
        await IssuerContinuationProviderFixture.RequireGrantAsync(scenario, attempt, login.Interaction, previous, grant);
    }

    /// <summary>Uses committed disposable data corruption only; production verification and proof construction remain unchanged.</summary>
    private static async Task BreakBindingAsync(JourneyScenario scenario, IssuerContinuationProviderFixture.Attempt attempt,
        string interaction, CompletionFailure failure)
    {
        if (failure == CompletionFailure.Expired)
        {
            scenario.Clock.Now = DateTimeOffset.FromUnixTimeSeconds((await IssuerContinuationProviderFixture.RowAsync(scenario, attempt)).ExpiresAt);
        }
        await using var scope = scenario.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        if (failure == CompletionFailure.MissingMarker)
        {
            var row = await database.Set<AdministratorInteraction>().SingleAsync(value => value.PublicId == interaction);
            row.BrowserIntentDigest = null;
        }
        if (failure == CompletionFailure.MissingRow)
        {
            database.Remove(await database.Set<IssuerContinuation>().SingleAsync(value => value.IntentDigest == attempt.Request.IntentDigest));
        }
        await database.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Discovers each password-boundary failure independently on both installed providers.</summary>
    public static IEnumerable<TheoryDataRow<string, BindingFailure>> BindingCases =>
        from provider in IssuerContinuationProviderFixture.Providers
        from failure in Enum.GetValues<BindingFailure>()
        select new TheoryDataRow<string, BindingFailure>(provider, failure).WithTrait(IssuerContinuationProviderFixture.ProviderTrait, provider);

    /// <summary>Discovers each persisted-completion failure independently on both installed providers.</summary>
    public static IEnumerable<TheoryDataRow<string, CompletionFailure>> CompletionCases =>
        from provider in IssuerContinuationProviderFixture.Providers
        from failure in Enum.GetValues<CompletionFailure>()
        select new TheoryDataRow<string, CompletionFailure>(provider, failure).WithTrait(IssuerContinuationProviderFixture.ProviderTrait, provider);
}

/// <summary>Names the independent password-binding denial boundaries.</summary>
public enum BindingFailure
{
    Browser, Purpose, Expired
}

/// <summary>Names the unchanged-overload denial boundaries, separating absent rows from missing interaction markers.</summary>
public enum CompletionFailure
{
    Browser, Purpose, Expired, MissingMarker, MissingRow
}
