using System.Net;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.Accounts;

/// <summary>Interrupts real HTTP enrollment/recovery after native writes reach their blocked audit INSERT and before authority can escape.</summary>
[Collection("administrator-bootstrap")]
public sealed class PrecommitFailureTests(BootstrapFixture fixture)
{
    private static readonly string[] Providers = ["PostgreSql", "SqlServer"];
    private static readonly bool[] Choices = [false, true];
    private const string ProviderTrait = "Provider";
    private const int DrainSeconds = 25;
    private const string PrematureResponse = "A sensitive response escaped before its blocked audit transaction committed.";

    /// <summary>Exercises database-session loss and actual AuthServer death separately for installation and restricted recovery.</summary>
    public static IEnumerable<TheoryDataRow<string, bool, bool>> Cases =>
        from provider in Providers
        from recovery in Choices
        from processDeath in Choices
        select new TheoryDataRow<string, bool, bool>(provider, recovery, processDeath).WithTrait(ProviderTrait, provider);

    /// <summary>Observed precommit interruption restores exact security state, delivers no cookie, and permits an explicit healthy-node retry.</summary>
    [Theory, MemberData(nameof(Cases))]
    public async Task Given_AuditBlockedAfterNativeWrites_When_ProcessOrDatabaseSessionDies_Then_NoAuthorityEscapesAndOriginalProofRemainsUsable(string provider, bool recovery, bool processDeath)
    {
        await using var environment = await AdministratorEnvironment.CreateAsync(fixture, provider);
        var cookies = new CookieContainer();
        using var browser = environment.Browser(cookies);
        var submission = await PrepareAsync(environment, browser, recovery);
        await using var observer = environment.Database.Administrator.CreateAsyncScope();
        var database = observer.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var before = await SecuritySnapshot.ReadAsync(database);
        using var request = Request(submission.Form, submission.Code, submission.Path);
        Task<HttpResponseMessage>? waiting = null;
        try
        {
            await using (var blocked = await GovernanceLock.AcquireAsync(environment.Database, audit: true))
            {
                waiting = browser.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, TestContext.Current.CancellationToken);
                await blocked.ObserveWaitersAsync(environment.Database, 1);
                Assert.False(waiting.IsCompleted, PrematureResponse);
                Assert.True(cookies.GetCookies(environment.Issuer)[AdministratorJourney.SessionCookie] is null, PrematureResponse);
                if (processDeath)
                {
                    await environment.KillPrimaryAsync();
                }
                else
                {
                    await blocked.TerminateAuditWaiterAsync(environment.Database);
                }
            }
            await RequireDeniedAsync(waiting);
            Assert.True(cookies.GetCookies(environment.Issuer)[AdministratorJourney.SessionCookie] is null, PrematureResponse);
            Assert.Equal(before, await SecuritySnapshot.ReadAsync(database));
            using var healthy = environment.Browser(cookies, secondary: true);
            using var retried = await AdministratorJourney.ConfirmAtAsync(healthy, submission.Form, submission.Code, submission.Path);
            Assert.Equal(recovery ? HttpStatusCode.Redirect : HttpStatusCode.OK, retried.StatusCode);
            Assert.Equal(!recovery, cookies.GetCookies(environment.Issuer)[AdministratorJourney.SessionCookie] is not null);
        }
        finally
        {
            browser.CancelPendingRequests();
            if (waiting is not null)
            {
                await DrainAsync(waiting);
            }
        }
    }

    /// <summary>Creates native password/form/proof bindings and leaves recovery's requesting browser entirely unauthenticated.</summary>
    private static async Task<Submission> PrepareAsync(AdministratorEnvironment environment, HttpClient browser, bool recovery)
    {
        string? recoveryCode = null;
        if (recovery)
        {
            using var owner = environment.Browser(new CookieContainer());
            recoveryCode = (await SecurityHttp.EnrollAsync(environment, owner)).RecoveryCodes[0];
        }
        var location = await AdministratorJourney.BeginAtAsync(environment, browser, recovery ? SecurityHttp.Recovery : AdministratorJourney.Enrollment);
        var form = await AdministratorJourney.ReadAsync(browser, location, enrollment: !recovery);
        return new Submission(form, recoveryCode ?? FactorOracle.Code(form.Seed!, DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
            recovery ? SecurityHttp.RecoveryVerify : AdministratorJourney.Enrollment);
    }

    /// <summary>Retains request/content ownership until the headers-only operation is drained.</summary>
    private static HttpRequestMessage Request(AdministratorForm form, string code, string path) => new(HttpMethod.Post, path)
    {
        Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            [AdministratorJourney.Interaction] = form.Interaction,
            [AdministratorJourney.Antiforgery] = form.Antiforgery,
            [AdministratorJourney.CodeField] = code
        })
    };

    /// <summary>Accepts only actual transport loss or server failure, never a successful sensitive response.</summary>
    private static async Task RequireDeniedAsync(Task<HttpResponseMessage> waiting)
    {
        try
        {
            using var response = await waiting;
            Assert.True((int)response.StatusCode >= (int)HttpStatusCode.InternalServerError);
        }
        catch (HttpRequestException) { /* The owned process was terminated before it could send headers. */ }
    }

    /// <summary>Drains the owned request under an independent deadline, disposing any returned response even on assertion failure.</summary>
    private static async Task DrainAsync(Task<HttpResponseMessage> waiting)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(DrainSeconds));
        _ = await Record.ExceptionAsync(async () => { using var response = await waiting.WaitAsync(deadline.Token); });
        Assert.True(waiting.IsCompleted);
    }

    /// <summary>Holds synthetic live proof solely in memory without a generated secret-bearing string representation.</summary>
    private sealed class Submission(AdministratorForm form, string code, string path)
    {
        internal AdministratorForm Form { get; } = form;
        internal string Code { get; } = code;
        internal string Path { get; } = path;
    }
}
