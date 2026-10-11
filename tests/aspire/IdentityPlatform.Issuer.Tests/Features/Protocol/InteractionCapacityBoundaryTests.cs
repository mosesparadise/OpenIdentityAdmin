using System.Net;
using IdentityPlatform.AuthServer.Features.Accounts.Login;
using IdentityPlatform.AuthServer.Features.Pilot;
using IdentityPlatform.AuthServer.Features.Protocol.Protection;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using IdentityPlatform.Issuer.Tests.Fixtures;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Issuer.Tests.Features.Protocol;

public sealed partial class InteractionCapacityTests
{
    [Theory]
    [InlineData("PostgreSql", false, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", true, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", false, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", true, Traits = ["Provider", "SqlServer"])]
    public async Task Empty_established_budget_shares_admission_but_cookie_free_starts_have_distinct_continuity(string provider, bool established)
    {
        var environment = await fixture.GetAsync(provider);
        var header = "";
        Cohort? cohort = null;
        if (established)
        {
            cohort = await SeedAsync(environment, 1);
            await using var scope = environment.Services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().Set<LoginInteraction>()
                .Where(value => value.Id == cohort.Interactions[0].Id).ExecuteUpdateAsync(update => update.SetProperty(value => value.Consumed, true), TestContext.Current.CancellationToken);
            header = cohort.Header(environment);
        }
        var before = await CountsAsync(environment);
        var budgets = await BudgetCountAsync(environment);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));
        var barrier = new InteractionAdmissionBarrier(deadline.Token);
        await using var first = environment.RuntimeServices(barrier);
        await using var second = environment.RuntimeServices(barrier);
        var results = await Task.WhenAll(AdmitAsync(first, header, deadline.Token), AdmitAsync(second, header, deadline.Token));
        Assert.Equal(2, barrier.Arrivals);
        Assert.True(results.All(value => value.Interaction is not null), "Both empty-budget admissions should succeed.");
        Assert.True(results[0].Interaction!.Id != results[1].Interaction!.Id, "Admissions reused an interaction identifier.");
        Assert.Equal(budgets + (established ? 0 : 2), await BudgetCountAsync(environment));
        if (established)
        {
            Assert.True(results.All(value => value.Interaction!.BudgetId == cohort!.Id), "Established continuity changed ownership.");
            Assert.Equal(2, await LiveAsync(environment, cohort!.Id));
        }
        else
        {
            Assert.True(results[0].Interaction!.BudgetId != results[1].Interaction!.BudgetId, "Cookie-free starts must not invent a shared browser identity.");
        }
        var after = await CountsAsync(environment);
        Assert.Equal(before.Interactions + 2, after.Interactions);
        Assert.Equal(before.Sessions, after.Sessions);
        Assert.Equal(before.Audits, after.Audits);
    }

    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Lone_admission_rendezvous_cancels_and_drains_without_allocating(string provider)
    {
        var environment = await fixture.GetAsync(provider);
        var cohort = await SeedAsync(environment, 1);
        var before = await CountsAsync(environment);
        var budget = await BudgetAsync(environment, cohort.Id);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(10));
        var barrier = new InteractionAdmissionBarrier(deadline.Token);
        await using var runtime = environment.RuntimeServices(barrier);
        var observed = new DefaultHttpContext();
        var attempt = Record.ExceptionAsync(async () => await AdmitAsync(runtime, cohort.Header(environment), CancellationToken.None, observed: observed)).AsTask();
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
        Assert.True(await attempt is OperationCanceledException, "A lone rendezvous must cancel through the harness deadline.");
        Assert.Equal(0, observed.Response.Headers.SetCookie.Count);
        Assert.Equal(before, await CountsAsync(environment));
        Assert.Equal(budget, await BudgetAsync(environment, cohort.Id));
        await using var recovered = environment.RuntimeServices();
        Assert.NotNull((await AdmitAsync(recovered, cohort.Header(environment), TestContext.Current.CancellationToken)).Interaction);
    }

    private static readonly string[] Providers = ["PostgreSql", "SqlServer"];
    private static readonly string[] InvalidBudgets = ["missing", "malformed", "unknown", "unknown-with-flow", "foreign", "mixed", "legacy", "expired"];
    public static IEnumerable<TheoryDataRow<string, string>> InvalidBudgetCases =>
        from provider in Providers
        from mode in InvalidBudgets
        select new TheoryDataRow<string, string>(provider, mode).WithTrait("Provider", provider);

    [Theory]
    [MemberData(nameof(InvalidBudgetCases))]
    public async Task Invalid_budget_continuity_cannot_adopt_live_interactions_or_allocate(string provider, string mode)
    {
        var environment = await fixture.GetAsync(provider);
        var clock = new AdmissionClock();
        var first = await SeedAsync(environment, 1, clock);
        var second = await SeedAsync(environment, 1, clock);
        var header = first.Header(environment);
        if (mode == "missing")
        {
            header = FlowCookie(first.Interactions[0], first, environment);
        }
        else if (mode is "malformed" or "unknown" or "unknown-with-flow")
        {
            header = PilotProfile.BudgetCookie + "=" + (mode == "malformed" ? "invalid" : InteractionSecrets.Create());
            if (mode == "unknown-with-flow")
            {
                header += "; " + FlowCookie(first.Interactions[0], first, environment);
            }
        }
        else if (mode == "foreign")
        {
            header = second.BudgetCookie(environment) + "; " + FlowCookie(first.Interactions[0], first, environment);
        }
        else if (mode == "mixed")
        {
            header = first.Header(environment) + "; " + FlowCookie(second.Interactions[0], second, environment);
        }
        else if (mode == "legacy")
        {
            await using var scope = environment.Services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().Set<LoginInteraction>()
                .Where(value => value.Id == first.Interactions[0].Id).ExecuteUpdateAsync(update => update.SetProperty(value => value.BudgetId, (string?)null), TestContext.Current.CancellationToken);
        }
        else
        {
            clock.Advance(300);
            header = first.BudgetCookie(environment);
        }
        var before = await CountsAsync(environment);
        var budgets = await BudgetCountAsync(environment);
        var firstBudget = await BudgetAsync(environment, first.Id);
        var secondBudget = await BudgetAsync(environment, second.Id);
        await using var runtime = environment.RuntimeServices(time: clock);
        var denied = await AdmitAsync(runtime, header, TestContext.Current.CancellationToken);
        Assert.True(denied.Interaction is null, "Denied admission or binding exposed unexpected state.");
        AssertNoAllocationCookies(denied.Cookies);
        Assert.Equal(before, await CountsAsync(environment));
        Assert.Equal(budgets, await BudgetCountAsync(environment));
        Assert.Equal(firstBudget, await BudgetAsync(environment, first.Id));
        Assert.Equal(secondBudget, await BudgetAsync(environment, second.Id));
        if (mode is "malformed" or "unknown" or "unknown-with-flow" or "expired")
        {
            Assert.True(denied.Cookies.Any(value => value!.StartsWith(PilotProfile.BudgetCookie + "=;", StringComparison.Ordinal)), "Invalid budget cookie was not expired.");
        }
        if (mode == "legacy")
        {
            await using var scope = environment.Services.CreateAsyncScope();
            Assert.True((await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().Set<LoginInteraction>()
                .AsNoTracking().SingleAsync(value => value.Id == first.Interactions[0].Id, TestContext.Current.CancellationToken)).BudgetId is null, "Denied admission or binding exposed unexpected state.");
        }
        else
        {
            await using var scope = environment.Services.CreateAsyncScope();
            var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            foreach (var original in new[] { first.Interactions[0], second.Interactions[0] })
            {
                var current = await database.Set<LoginInteraction>().AsNoTracking().SingleAsync(value => value.Id == original.Id, TestContext.Current.CancellationToken);
                Assert.True(current.BudgetId == original.BudgetId && current.BrowserDigest == original.BrowserDigest &&
                    current.RequestTokenId == original.RequestTokenId && current.RequestUri == original.RequestUri &&
                    current.ClientId == original.ClientId && current.CreatedAt == original.CreatedAt && current.ExpiresAt == original.ExpiresAt &&
                    current.CompletedSessionId == original.CompletedSessionId && current.CompletedUserId == original.CompletedUserId && current.Consumed == original.Consumed,
                    "Denied admission rewrote an existing interaction or its ownership/proofs.");
            }
        }
        // A separate, cookie-free budget remains usable; no global lock or ownership adoption.
        var fresh = await AdmitAsync(runtime, "", TestContext.Current.CancellationToken);
        Assert.NotNull(fresh.Interaction);
        Assert.True(fresh.Interaction.BudgetId != first.Id && fresh.Interaction.BudgetId != second.Id, "Fresh admission adopted another budget.");
    }

    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Missing_or_forged_flow_cookies_do_not_hide_live_rows_or_authenticate_an_interaction(string provider)
    {
        var environment = await fixture.GetAsync(provider);
        var cohort = await SeedAsync(environment, 8);
        await using var runtime = environment.RuntimeServices();
        var before = await CountsAsync(environment);
        var budget = await BudgetAsync(environment, cohort.Id);
        var forged = cohort.BudgetCookie(environment) + "; " + PilotProfile.CorrelationPrefix + cohort.Interactions[0].Id + "=" + InteractionSecrets.Create();
        var denied = await AdmitAsync(runtime, forged, TestContext.Current.CancellationToken);
        Assert.True(denied.Interaction is null, "Denied admission or binding exposed unexpected state.");
        AssertNoAllocationCookies(denied.Cookies);
        Assert.Equal(before, await CountsAsync(environment));
        Assert.Equal(budget, await BudgetAsync(environment, cohort.Id));
        await using var scope = runtime.CreateAsyncScope();
        PilotDatabaseBounds.Apply(scope.ServiceProvider.GetRequiredService<PlatformDbContext>());
        var context = new DefaultHttpContext { RequestAborted = TestContext.Current.CancellationToken };
        context.Request.Headers.Cookie = cohort.BudgetCookie(environment);
        var interactions = scope.ServiceProvider.GetRequiredService<LoginInteractions>();
        Assert.True(await interactions.BoundAsync(context, cohort.Interactions[0].Id) is null, "Denied admission or binding exposed unexpected state.");
        context.Request.Headers.Cookie = forged;
        Assert.True(await interactions.BoundAsync(context, cohort.Interactions[0].Id) is null, "Denied admission or binding exposed unexpected state.");
        context.Request.Headers.Cookie = cohort.Header(environment);
        Assert.NotNull(await interactions.BoundAsync(context, cohort.Interactions[0].Id));
    }

    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Exact_interaction_expiry_frees_one_slot_and_only_success_refreshes_the_budget(string provider)
    {
        var environment = await fixture.GetAsync(provider);
        var clock = new AdmissionClock();
        var cohort = await SeedAsync(environment, 8, clock);
        var original = await BudgetAsync(environment, cohort.Id);
        clock.Advance(60);
        await using var runtime = environment.RuntimeServices(time: clock);
        Assert.True((await AdmitAsync(runtime, cohort.Header(environment), TestContext.Current.CancellationToken)).Interaction is null, "Denied admission or binding exposed unexpected state.");
        Assert.Equal(original, await BudgetAsync(environment, cohort.Id));
        await using (var scope = environment.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().Set<LoginInteraction>()
                .Where(value => value.Id == cohort.Interactions[0].Id).ExecuteUpdateAsync(update => update.SetProperty(value => value.ExpiresAt, clock.Seconds), TestContext.Current.CancellationToken);
        }
        var admitted = await AdmitAsync(runtime, cohort.Header(environment), TestContext.Current.CancellationToken);
        Assert.NotNull(admitted.Interaction);
        Assert.Equal(clock.Seconds, admitted.Interaction.CreatedAt);
        Assert.Equal(clock.Seconds + 300, admitted.Interaction.ExpiresAt);
        var updated = await BudgetAsync(environment, cohort.Id);
        Assert.Equal(new BudgetState(clock.Seconds, clock.Seconds + 300), updated);
        Assert.True(admitted.Cookies.Count(value => value!.Contains("max-age=300", StringComparison.OrdinalIgnoreCase)) == 2,
            "Successful admission must refresh budget and correlation cookies.");
        Assert.True(admitted.Cookies.Where(value => !value!.Contains("=;", StringComparison.Ordinal)).All(value =>
            value!.Contains("path=/", StringComparison.OrdinalIgnoreCase) && value.Contains("secure", StringComparison.OrdinalIgnoreCase) &&
            value.Contains("httponly", StringComparison.OrdinalIgnoreCase) && value.Contains("samesite=lax", StringComparison.OrdinalIgnoreCase) &&
            !value.Contains("domain=", StringComparison.OrdinalIgnoreCase)), "Admission cookies lost host-only protections.");
        Assert.Equal(8, await LiveAsync(environment, cohort.Id, clock.Seconds));
        // An old snapshot still omits the committed winner, including after a lost response.
        Assert.True((await AdmitAsync(runtime, cohort.Header(environment), TestContext.Current.CancellationToken)).Interaction is null, "Denied admission or binding exposed unexpected state.");
        Assert.Equal(updated, await BudgetAsync(environment, cohort.Id));
    }

    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Genuine_legacy_flow_can_complete_without_adopting_a_budget(string provider)
    {
        var environment = await fixture.GetAsync(provider);
        var cookies = new CookieContainer();
        using var browser = IssuerEnvironment.CreateBrowserClient(cookies);
        var login = await TestLogin.BeginAsync(environment, browser, exchange: true);
        await using (var scope = environment.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().Set<LoginInteraction>()
                .Where(value => value.Id == login.Interaction).ExecuteUpdateAsync(update => update.SetProperty(value => value.BudgetId, (string?)null), TestContext.Current.CancellationToken);
        }
        await using var runtime = environment.RuntimeServices();
        Assert.True((await AdmitAsync(runtime, cookies.GetCookieHeader(environment.Issuer), TestContext.Current.CancellationToken)).Interaction is null, "Denied admission or binding exposed unexpected state.");
        cookies.GetCookies(environment.Issuer)[PilotProfile.BudgetCookie]!.Expired = true;
        await login.FollowAsync(environment, browser, await login.CompleteAsync(environment, browser));
        await using var inspection = environment.Services.CreateAsyncScope();
        var row = await inspection.ServiceProvider.GetRequiredService<PlatformDbContext>().Set<LoginInteraction>().AsNoTracking()
            .SingleAsync(value => value.Id == login.Interaction, TestContext.Current.CancellationToken);
        Assert.True(row.Consumed && row.BudgetId is null && login.Flow.Validated, "Legacy completion must retain its original proofs without adopting ownership.");
    }

    private static async Task<Cohort> SeedAsync(IssuerEnvironment environment, int count, TimeProvider? time = null)
    {
        var cohort = new Cohort();
        await using var runtime = environment.RuntimeServices(time: time);
        for (var index = 0; index < count; index++)
        {
            var result = await AdmitAsync(runtime, cohort.Header(environment), TestContext.Current.CancellationToken);
            Assert.NotNull(result.Interaction);
            cohort.Interactions.Add(result.Interaction);
            foreach (var cookie in result.Cookies)
            {
                cohort.Cookies.SetCookies(environment.Issuer, cookie!);
            }
        }
        return cohort;
    }

    private static string FlowCookie(LoginInteraction row, Cohort cohort, IssuerEnvironment environment) =>
        PilotProfile.CorrelationPrefix + row.Id + "=" + cohort.Cookies.GetCookies(environment.Issuer)[PilotProfile.CorrelationPrefix + row.Id]!.Value;

    private static async Task<int> LiveAsync(IssuerEnvironment environment, string budgetId, long? now = null)
    {
        await using var scope = environment.Services.CreateAsyncScope();
        var current = now ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().Set<LoginInteraction>()
            .CountAsync(value => value.BudgetId == budgetId && !value.Consumed && value.ExpiresAt > current, TestContext.Current.CancellationToken);
    }

    private static async Task<int> BudgetCountAsync(IssuerEnvironment environment)
    {
        await using var scope = environment.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().Set<LoginBrowserBudget>().CountAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<BudgetState> BudgetAsync(IssuerEnvironment environment, string id)
    {
        await using var scope = environment.Services.CreateAsyncScope();
        var row = await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().Set<LoginBrowserBudget>().AsNoTracking()
            .SingleAsync(value => value.Id == id, TestContext.Current.CancellationToken);
        return new BudgetState(row.LastAdmissionAt, row.ExpiresAt);
    }

    private static void AssertNoAllocationCookies(string?[] cookies) => Assert.True(cookies.All(value => value!.Contains("=;", StringComparison.Ordinal)),
        "Denied admission emitted a new credential instead of only expiring invalid cookies.");

    private sealed record BudgetState(long LastAdmissionAt, long ExpiresAt);
    private sealed class Cohort
    {
        public CookieContainer Cookies { get; } = new();
        public List<LoginInteraction> Interactions { get; } = [];
        public string Id => Interactions[0].BudgetId!;
        public string Header(IssuerEnvironment environment) => Cookies.GetCookieHeader(environment.Issuer);
        public string BudgetCookie(IssuerEnvironment environment) => PilotProfile.BudgetCookie + "=" + Cookies.GetCookies(environment.Issuer)[PilotProfile.BudgetCookie]!.Value;
    }
    private sealed class AdmissionClock : TimeProvider
    {
        private long seconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        public long Seconds => Interlocked.Read(ref seconds);
        public void Advance(int value) => Interlocked.Add(ref seconds, value);
        public override DateTimeOffset GetUtcNow() => DateTimeOffset.FromUnixTimeSeconds(Seconds);
    }
}
