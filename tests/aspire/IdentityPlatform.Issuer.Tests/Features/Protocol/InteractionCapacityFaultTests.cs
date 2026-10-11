using System.Data.Common;
using System.Net;
using IdentityPlatform.AuthServer.Features.Accounts.Login;
using IdentityPlatform.AuthServer.Features.Protocol.Protection;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using IdentityPlatform.Issuer.Tests.Fixtures;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Issuer.Tests.Features.Protocol;

public sealed partial class InteractionCapacityTests
{
    [Theory]
    [InlineData("PostgreSql", false, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", true, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", false, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", true, Traits = ["Provider", "SqlServer"])]
    public async Task Expiry_during_the_count_query_is_rechecked_before_admission(string provider, bool budgetExpiry)
    {
        var environment = await fixture.GetAsync(provider);
        var clock = new AdmissionClock();
        var cohort = await SeedAsync(environment, 1, clock);
        var before = await CountsAsync(environment);
        var budget = await BudgetAsync(environment, cohort.Id);
        var observer = new CountClockAdvance(() => clock.Advance(budgetExpiry ? 300 : 1));
        await using var runtime = environment.RuntimeServices(observer, clock);
        var denied = await AdmitAsync(runtime, cohort.Header(environment), TestContext.Current.CancellationToken,
            nativeExpiry: clock.Seconds + (budgetExpiry ? 600 : 1));
        Assert.Equal(1, observer.Counts);
        Assert.True(denied.Interaction is null, "Denied admission or binding exposed unexpected state.");
        AssertNoAllocationCookies(denied.Cookies);
        Assert.Equal(before, await CountsAsync(environment));
        Assert.Equal(budget, await BudgetAsync(environment, cohort.Id));
        await using var recovered = environment.RuntimeServices(time: clock);
        Assert.NotNull((await AdmitAsync(recovered, budgetExpiry ? "" : cohort.BudgetCookie(environment), TestContext.Current.CancellationToken)).Interaction);
    }

    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Stale_tracked_budget_cannot_replace_current_persisted_expiry(string provider)
    {
        var environment = await fixture.GetAsync(provider);
        var cohort = await SeedAsync(environment, 1);
        await using var runtime = environment.RuntimeServices();
        await using var scope = runtime.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        PilotDatabaseBounds.Apply(database);
        var tracked = await database.Set<LoginBrowserBudget>().SingleAsync(value => value.Id == cohort.Id, TestContext.Current.CancellationToken);
        await using (var writer = environment.Services.CreateAsyncScope())
        {
            await writer.ServiceProvider.GetRequiredService<PlatformDbContext>().Set<LoginBrowserBudget>()
                .Where(value => value.Id == cohort.Id).ExecuteUpdateAsync(update => update.SetProperty(value => value.ExpiresAt, 1), TestContext.Current.CancellationToken);
        }
        Assert.True(tracked.ExpiresAt > 1, "The control must retain stale tracked budget state.");
        var before = await CountsAsync(environment);
        var context = new DefaultHttpContext { RequestAborted = TestContext.Current.CancellationToken, RequestServices = scope.ServiceProvider };
        context.Request.Headers.Cookie = cohort.Header(environment);
        var denied = await scope.ServiceProvider.GetRequiredService<LoginInteractions>().CreateAsync(context,
            InteractionSecrets.Create(), "urn:synthetic:tracked-budget", DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 300);
        Assert.True(denied is null, "Denied admission or binding exposed unexpected state.");
        AssertNoAllocationCookies(context.Response.Headers.SetCookie.ToArray());
        Assert.Equal(before, await CountsAsync(environment));
        Assert.Equal(1, (await BudgetAsync(environment, cohort.Id)).ExpiresAt);
    }

    private sealed class CountClockAdvance(Action advance) : DbCommandInterceptor
    {
        public int Counts
        {
            get; private set;
        }
        public override ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
            DbDataReader result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("COUNT(*)", StringComparison.OrdinalIgnoreCase) &&
                command.CommandText.Contains("LoginInteraction", StringComparison.Ordinal))
            {
                Counts++;
                advance();
            }
            return ValueTask.FromResult(result);
        }
    }

    [Theory]
    [InlineData("PostgreSql", false, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", true, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", false, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", true, Traits = ["Provider", "SqlServer"])]
    public async Task Insert_failure_rolls_back_existing_or_new_budget_without_emitting_cookies(string provider, bool newBudget)
    {
        var environment = await fixture.GetAsync(provider);
        var clock = new AdmissionClock();
        var cohort = await SeedAsync(environment, 1, clock);
        var before = await CountsAsync(environment);
        var budgetCount = await BudgetCountAsync(environment);
        var budget = await BudgetAsync(environment, cohort.Id);
        clock.Advance(1);
        await using var runtime = environment.RuntimeServices(time: clock);
        var context = new DefaultHttpContext();
        var error = await Record.ExceptionAsync(async () => await AdmitAsync(runtime, newBudget ? "" : cohort.Header(environment),
            TestContext.Current.CancellationToken, requestToken: cohort.Interactions[0].RequestTokenId, observed: context));
        Assert.True(error is DbUpdateException, "The native unique request constraint must reject duplicate admission.");
        Assert.Equal(0, context.Response.Headers.SetCookie.Count);
        Assert.Equal(before, await CountsAsync(environment));
        Assert.Equal(budgetCount, await BudgetCountAsync(environment));
        Assert.Equal(budget, await BudgetAsync(environment, cohort.Id));
        Assert.NotNull((await AdmitAsync(runtime, newBudget ? "" : cohort.Header(environment), TestContext.Current.CancellationToken)).Interaction);
    }

    [Theory]
    [InlineData("PostgreSql", false, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", true, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", false, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", true, Traits = ["Provider", "SqlServer"])]
    public async Task Native_or_budget_expiry_while_waiting_for_the_lock_denies_without_refresh(string provider, bool budgetExpiry)
    {
        var environment = await fixture.GetAsync(provider);
        var clock = new AdmissionClock();
        var cohort = await SeedAsync(environment, 1, clock);
        var before = await CountsAsync(environment);
        var budget = await BudgetAsync(environment, cohort.Id);
        await using var held = await DatabaseRowLock.AcquireBudgetAsync(environment, provider, cohort.Id);
        await using var runtime = environment.RuntimeServices(time: clock);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(10));
        Admission? admission = null;
        var context = new DefaultHttpContext();
        var attempt = Record.ExceptionAsync(async () => admission = await AdmitAsync(runtime, cohort.BudgetCookie(environment), deadline.Token,
            nativeExpiry: clock.Seconds + (budgetExpiry ? 600 : 1), observed: context)).AsTask();
        DatabaseRowLock.BlockedCommand? waiting = null;
        try
        {
            waiting = await held.WaitForRuntimeAsync(deadline.Token);
            Assert.True(waiting.IsUpdate, "Admission must be blocked on its budget update.");
            clock.Advance(budgetExpiry ? 300 : 1);
            await held.ReleaseAsync();
            Assert.True(await attempt is null, "A valid lock wait should end in a safe admission denial, not an infrastructure exception.");
            Assert.NotNull(admission);
            Assert.True(admission.Interaction is null, "Denied admission or binding exposed unexpected state.");
            AssertNoAllocationCookies(admission.Cookies);
        }
        finally
        {
            await deadline.CancelAsync();
            await attempt;
            try
            {
                await DrainAsync(environment, held, waiting);
            }
            finally { await held.ReleaseAsync(); }
        }
        Assert.Equal(before, await CountsAsync(environment));
        Assert.Equal(budget, await BudgetAsync(environment, cohort.Id));
        var recovered = await AdmitAsync(runtime, budgetExpiry ? "" : cohort.BudgetCookie(environment), TestContext.Current.CancellationToken);
        Assert.NotNull(recovered.Interaction);
    }

    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Waiting_admission_cancels_without_writes_while_an_independent_budget_progresses(string provider)
    {
        var environment = await fixture.GetAsync(provider);
        var blocked = await SeedAsync(environment, 1);
        var independent = await SeedAsync(environment, 1);
        var before = await CountsAsync(environment);
        var budget = await BudgetAsync(environment, blocked.Id);
        await using var held = await DatabaseRowLock.AcquireBudgetAsync(environment, provider, blocked.Id);
        await using var runtime = environment.RuntimeServices();
        await using var other = environment.RuntimeServices();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(10));
        var context = new DefaultHttpContext();
        var attempt = Record.ExceptionAsync(async () => await AdmitAsync(runtime, blocked.BudgetCookie(environment), deadline.Token, observed: context)).AsTask();
        DatabaseRowLock.BlockedCommand? waiting = null;
        try
        {
            waiting = await held.WaitForRuntimeAsync(deadline.Token);
            Assert.True(waiting.IsUpdate, "Admission must be blocked on its budget update.");
            Assert.NotNull((await AdmitAsync(other, independent.BudgetCookie(environment), deadline.Token)).Interaction);
            await deadline.CancelAsync();
            Assert.True(await attempt is OperationCanceledException, "A blocked admission must observe cancellation.");
            Assert.Equal(0, context.Response.Headers.SetCookie.Count);
        }
        finally
        {
            await deadline.CancelAsync();
            await attempt;
            try
            {
                await DrainAsync(environment, held, waiting);
            }
            finally { await held.ReleaseAsync(); }
        }
        var after = await CountsAsync(environment);
        Assert.Equal(before.Interactions + 1, after.Interactions);
        Assert.Equal(before.Sessions, after.Sessions);
        Assert.Equal(before.Audits, after.Audits);
        Assert.Equal(budget, await BudgetAsync(environment, blocked.Id));
        Assert.NotNull((await AdmitAsync(other, blocked.Header(environment), TestContext.Current.CancellationToken)).Interaction);
    }

    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Completed_and_failed_consumption_stay_charged_until_genuine_committed_consumption(string provider)
    {
        var environment = await fixture.GetAsync(provider);
        var cookies = new CookieContainer();
        using var browser = IssuerEnvironment.CreateBrowserClient(cookies);
        using var controlled = IssuerEnvironment.CreateBrowserClient(cookies, autoRedirect: false);
        var login = await TestLogin.BeginAsync(environment, browser, exchange: true);
        await using var runtime = environment.RuntimeServices();
        for (var index = 1; index < 8; index++)
        {
            var result = await AdmitAsync(runtime, cookies.GetCookieHeader(environment.Issuer), TestContext.Current.CancellationToken);
            Assert.NotNull(result.Interaction);
            foreach (var cookie in result.Cookies)
            {
                cookies.SetCookies(environment.Issuer, cookie!);
            }
        }
        var continuation = await login.CompleteAsync(environment, browser);
        var header = cookies.GetCookieHeader(environment.Issuer);
        var before = await CountsAsync(environment);
        Assert.True((await AdmitAsync(runtime, header, TestContext.Current.CancellationToken)).Interaction is null, "Denied admission or binding exposed unexpected state.");
        await using (await RuntimeInsertDenial.CreateAsync(environment, provider))
        {
            using var denied = await controlled.GetAsync(continuation, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, denied.StatusCode);
        }
        Assert.True((await AdmitAsync(runtime, header, TestContext.Current.CancellationToken)).Interaction is null, "Denied admission or binding exposed unexpected state.");
        Assert.Equal(before, await CountsAsync(environment));
        Assert.True(string.IsNullOrEmpty(login.Flow.Code), "Failed consumption issued a code.");
        await login.FollowAsync(environment, browser, continuation);
        var admitted = await AdmitAsync(runtime, cookies.GetCookieHeader(environment.Issuer), TestContext.Current.CancellationToken);
        Assert.NotNull(admitted.Interaction);
        Assert.Equal(8, await LiveAsync(environment, admitted.Interaction.BudgetId!));
        Assert.True(login.Flow.Validated, "Genuine consumption did not complete independent token validation.");
    }

    private static async Task DrainAsync(IssuerEnvironment environment, DatabaseRowLock held, DatabaseRowLock.BlockedCommand? waiting)
    {
        using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        try
        {
            if (waiting is not null)
            {
                await held.DrainAsync(waiting, cleanup.Token);
            }
            else
            {
                environment.Invalidate();
            }
        }
        catch
        {
            environment.Invalidate();
            throw;
        }
    }
}
