using System.Security.Cryptography;
using System.Text;
using IdentityPlatform.AuthServer.Features.Accounts.Login;
using IdentityPlatform.AuthServer.Features.Protocol.Protection;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using IdentityPlatform.Issuer.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Issuer.Tests.Features.Protocol;

[Collection("issuer")]
public sealed class InteractionRaceTests(IssuerFixture fixture)
{
    private static readonly string[] Providers = ["PostgreSql", "SqlServer"];
    private static readonly string[] Modes = ["complete", "consume-pending", "consume-completed", "mixed"];
    public static IEnumerable<TheoryDataRow<string, string>> Cases =>
        from provider in Providers
        from mode in Modes
        select new TheoryDataRow<string, string>(provider, mode).WithTrait("Provider", provider);

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Conditional_transitions_have_one_complete_winner_at_the_database_boundary(string provider, string mode)
    {
        var environment = await fixture.GetAsync(provider);
        using var browser = IssuerEnvironment.CreateBrowserClient();
        // An ordinary pending request can reuse a session obtained by a different genuine flow.
        var pending = mode is "consume-pending" or "mixed" ? await TestLogin.BeginAsync(environment, browser) : null;
        var original = await environment.AuthorizeAsync(browser, exchange: true);
        var login = pending ?? await TestLogin.BeginAsync(environment, browser, prompt: "login");
        var sessionId = original.ExpectedSessionId!;
        if (mode == "consume-completed")
        {
            await login.CompleteAsync(environment, browser);
            sessionId = login.Flow.ExpectedSessionId!;
        }
        var before = await SnapshotAsync(environment, login.Interaction, sessionId);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(30));
        var barrier = new InteractionUpdateBarrier(login.Interaction, deadline.Token);
        await using var services = environment.RuntimeServices(barrier);
        var results = await Task.WhenAll(
            TransitionAsync(environment, services, login.Interaction, sessionId, complete: mode is "complete" or "mixed", deadline.Token),
            TransitionAsync(environment, services, login.Interaction, sessionId, complete: mode == "complete", deadline.Token));
        Assert.Equal(2, barrier.Arrivals);
        Assert.True(results.Count(value => value.Session is not null || value.Consumed) == 1, "Exactly one conditional transition must win.");
        var after = await SnapshotAsync(environment, login.Interaction, sessionId);
        var winningSession = results.SingleOrDefault(value => value.Session is not null)?.Session;
        if (winningSession is not null)
        {
            Assert.True(mode is "complete" or "mixed", "A consumption-only race created a session.");
            Assert.True(after.Interaction.CompletedSessionId == winningSession.Id && after.Interaction.CompletedUserId == winningSession.UserId &&
                !after.Interaction.Consumed && after.Previous.Revoked, "Completion did not atomically bind the new session and revoke its predecessor.");
            Assert.Equal(before.Sessions + 1, after.Sessions);
            await using var scope = environment.Services.CreateAsyncScope();
            var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            var persisted = await database.Set<LocalIdentitySession>().AsNoTracking().SingleAsync(value => value.Id == winningSession.Id, deadline.Token);
            Assert.True(persisted.UserId == winningSession.UserId && persisted.SecurityStamp == winningSession.SecurityStamp &&
                persisted.AuthenticatedAt == winningSession.AuthenticatedAt && persisted.ExpiresAt == winningSession.ExpiresAt && !persisted.Revoked,
                "Winning session fields were not persisted coherently.");
            AssertAudits(before, after, created: 1, replaced: 1, approved: 0);
        }
        else
        {
            Assert.True(mode != "complete" && after.Interaction.Consumed && !after.Previous.Revoked, "Consumption must preserve the eligible existing session.");
            Assert.True(after.Interaction.CompletedSessionId == before.Interaction.CompletedSessionId &&
                after.Interaction.CompletedUserId == before.Interaction.CompletedUserId, "Consumption changed completion ownership.");
            Assert.Equal(before.Sessions, after.Sessions);
            AssertAudits(before, after, created: 0, replaced: 0, approved: 1);
            await using var retry = environment.RuntimeServices();
            var replay = await TransitionAsync(environment, retry, login.Interaction, sessionId, complete: false, deadline.Token);
            Assert.False(replay.Consumed);
            var unchanged = await SnapshotAsync(environment, login.Interaction, sessionId);
            Assert.True(unchanged.Audits.Select(value => value.Id).ToHashSet().SetEquals(after.Audits.Select(value => value.Id)),
                "Consumption replay appended an audit event.");
        }
        // These are DB transition proofs; no authentication cookie/native code is fabricated for a component winner.
        Assert.True(string.IsNullOrEmpty(login.Flow.Code), "Component transitions must not fabricate protocol responses.");
    }

    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Completed_interaction_consumption_requires_its_exact_new_session(string provider)
    {
        var environment = await fixture.GetAsync(provider);
        using var browser = IssuerEnvironment.CreateBrowserClient();
        var original = await environment.AuthorizeAsync(browser, exchange: true);
        var login = await TestLogin.BeginAsync(environment, browser, prompt: "login", exchange: true);
        var continuation = await login.CompleteAsync(environment, browser);
        var before = await SnapshotAsync(environment, login.Interaction, original.ExpectedSessionId!);
        await using var runtime = environment.RuntimeServices();
        var denied = await TransitionAsync(environment, runtime, login.Interaction, original.ExpectedSessionId!, complete: false, TestContext.Current.CancellationToken);
        Assert.False(denied.Consumed);
        var after = await SnapshotAsync(environment, login.Interaction, original.ExpectedSessionId!);
        Assert.Equal(before.Sessions, after.Sessions);
        Assert.True(!after.Interaction.Consumed && after.Interaction.CompletedSessionId == before.Interaction.CompletedSessionId &&
            after.Interaction.CompletedUserId == before.Interaction.CompletedUserId && after.Previous.Revoked == before.Previous.Revoked,
            "Wrong-session consumption changed the completed interaction.");
        AssertAudits(before, after, created: 0, replaced: 0, approved: 0);
        // The genuine current cookie resumes with N and proves the rejected S attempt did not consume it.
        await login.FollowAsync(environment, browser, continuation);
        var accepted = await SnapshotAsync(environment, login.Interaction, original.ExpectedSessionId!);
        Assert.True(accepted.Interaction.Consumed);
        Assert.Equal(before.Sessions, accepted.Sessions);
        AssertAudits(after, accepted, created: 0, replaced: 0, approved: 1);
    }

    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Cancelled_transition_rendezvous_drains_work_and_preserves_a_genuine_retry(string provider)
    {
        var environment = await fixture.GetAsync(provider);
        using var browser = IssuerEnvironment.CreateBrowserClient();
        var original = await environment.AuthorizeAsync(browser, exchange: true);
        var login = await TestLogin.BeginAsync(environment, browser, prompt: "login", exchange: true);
        var before = await SnapshotAsync(environment, login.Interaction, original.ExpectedSessionId!);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(15));
        var barrier = new InteractionUpdateBarrier(login.Interaction, deadline.Token);
        await using var services = environment.RuntimeServices(barrier);
        var attempt = Record.ExceptionAsync(async () =>
            await TransitionAsync(environment, services, login.Interaction, original.ExpectedSessionId!, complete: true, CancellationToken.None)).AsTask();
        try
        {
            await barrier.FirstArrival.WaitAsync(deadline.Token);
            Assert.Equal(1, barrier.Arrivals);
        }
        finally
        {
            await deadline.CancelAsync();
            await attempt;
        }
        Assert.True(await attempt is OperationCanceledException, "A lone transition must cancel cooperatively without abandoned work.");
        var after = await SnapshotAsync(environment, login.Interaction, original.ExpectedSessionId!);
        Assert.Equal(before.Sessions, after.Sessions);
        Assert.True(after.Interaction.CompletedSessionId is null && after.Interaction.CompletedUserId is null && !after.Interaction.Consumed && !after.Previous.Revoked,
            "Cancellation left a partial security transition.");
        AssertAudits(before, after, created: 0, replaced: 0, approved: 0);
        await login.FollowAsync(environment, browser, await login.CompleteAsync(environment, browser));
    }

    private static async Task<Transition> TransitionAsync(IssuerEnvironment environment, IServiceProvider services, string interactionId,
        string previousSessionId, bool complete, CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        PilotDatabaseBounds.Apply(database);
        var interaction = await database.Set<LoginInteraction>().AsNoTracking().SingleAsync(value => value.Id == interactionId, cancellationToken);
        var transitions = scope.ServiceProvider.GetRequiredService<LoginInteractions>();
        if (complete)
        {
            return new Transition { Session = await transitions.CompleteAsync(interaction, environment.Username, environment.Password, previousSessionId, cancellationToken) };
        }
        var session = await database.Set<LocalIdentitySession>().AsNoTracking().SingleAsync(value => value.Id == previousSessionId, cancellationToken);
        return new Transition { Consumed = await transitions.ConsumeAsync(interaction, session, cancellationToken) };
    }

    private static async Task<State> SnapshotAsync(IssuerEnvironment environment, string interactionId, string sessionId)
    {
        await using var scope = environment.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var token = TestContext.Current.CancellationToken;
        return new State
        {
            Interaction = await database.Set<LoginInteraction>().AsNoTracking().SingleAsync(value => value.Id == interactionId, token),
            Previous = await database.Set<LocalIdentitySession>().AsNoTracking().SingleAsync(value => value.Id == sessionId, token),
            Sessions = await database.Set<LocalIdentitySession>().CountAsync(token),
            Audits = await database.Set<SecurityAuditIntent>().AsNoTracking().ToArrayAsync(token)
        };
    }

    private static void AssertAudits(State before, State after, int created, int replaced, int approved)
    {
        Assert.True(before.Interaction.Id == after.Interaction.Id && before.Interaction.RequestTokenId == after.Interaction.RequestTokenId &&
            before.Interaction.RequestUri == after.Interaction.RequestUri && before.Interaction.ClientId == after.Interaction.ClientId &&
            before.Interaction.BrowserDigest == after.Interaction.BrowserDigest && before.Interaction.CreatedAt == after.Interaction.CreatedAt &&
            before.Interaction.ExpiresAt == after.Interaction.ExpiresAt, "A transition changed immutable request bindings.");
        Assert.True(before.Previous.Id == after.Previous.Id && before.Previous.UserId == after.Previous.UserId &&
            before.Previous.SecurityStamp == after.Previous.SecurityStamp && before.Previous.AuthenticatedAt == after.Previous.AuthenticatedAt &&
            before.Previous.ExpiresAt == after.Previous.ExpiresAt, "A transition rewrote predecessor session fields.");
        Assert.True(before.Audits.All(original => after.Audits.Any(current =>
            current.Id == original.Id && current.Event == original.Event && current.Outcome == original.Outcome &&
            current.ActorType == original.ActorType && current.ActorId == original.ActorId && current.TargetType == original.TargetType &&
            current.TargetId == original.TargetId && current.CorrelationId == original.CorrelationId && current.OccurredAt == original.OccurredAt)),
            "A transition changed an existing audit event.");
        var ids = before.Audits.Select(value => value.Id).ToHashSet();
        var added = after.Audits.Where(value => !ids.Contains(value.Id)).ToArray();
        Assert.Equal(created + replaced + approved, added.Length);
        Assert.Equal(created, added.Count(value => value.Event == "password-verification" && value.Outcome == "session-created"));
        Assert.Equal(replaced, added.Count(value => value.Event == "session-replacement" && value.Outcome == "revoked"));
        Assert.Equal(approved, added.Count(value => value.Event == "authorization-decision" && value.Outcome == "approved-for-native-processing"));
        Assert.True(added.All(value => value.ActorType == "account" && value.ActorId == after.Previous.UserId && value.CorrelationId != Guid.Empty),
            "Security transition audits must retain account attribution.");
        var sessionDigest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(before.Previous.Id)));
        Assert.True(added.All(value => value.OccurredAt >= before.Interaction.CreatedAt &&
            (value.Event == "session-replacement" ? value.TargetType == "local-session-digest" && value.TargetId == sessionDigest :
                value.TargetType == "pilot-account" && value.TargetId == after.Previous.UserId)), "Audit targets and timestamps must match the transition.");
    }

    private sealed class Transition
    {
        public LocalIdentitySession? Session
        {
            get; init;
        }
        public bool Consumed
        {
            get; init;
        }
    }

    private sealed class State
    {
        public required LoginInteraction Interaction
        {
            get; init;
        }
        public required LocalIdentitySession Previous
        {
            get; init;
        }
        public required SecurityAuditIntent[] Audits
        {
            get; init;
        }
        public int Sessions
        {
            get; init;
        }
    }
}
