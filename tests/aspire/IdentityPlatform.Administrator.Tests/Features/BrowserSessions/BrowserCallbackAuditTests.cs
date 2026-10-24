using System.Net;
using System.Runtime.ExceptionServices;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Sessions.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Requires actual provider audit denial after native state consumption and successful token exchange.</summary>
[Collection("administrator-bootstrap")]
public sealed class BrowserCallbackAuditTests(BootstrapFixture fixture)
{
    private const string ProviderTrait = "Provider";

    private static readonly string[] ProviderNames = ["PostgreSql", "SqlServer"];
    private static readonly bool[] TicketVariants = [false, true];
    private const int CleanupSeconds = 10;

    /// <summary>Each local audit failure rolls back establishment, leaves native state spent, and allows only a new explicit sign-in after restoration.</summary>
    [Theory, MemberData(nameof(Variants))]
    public async Task Given_ActualAuditInsertDenied_When_ProductCallbackEstablishes_Then_LocalTransactionRollsBackWithoutNativeReplay(string provider, bool ticketAudit)
    {
        var observation = new BrowserLoginAuditObservation();
        await using var scenario = await BrowserLoginFixture.CreateAsync(fixture, provider, services =>
            services.AddDbContext<AdminSessionDbContext>((_, options) => options.AddInterceptors(observation)));
        var callback = await scenario.CallbackAsync();
        var cookies = scenario.Cookies.GetCookieHeader(callback);
        var access = ticketAudit ? scenario.Database.Access.SetTicketAuditAsync : (Func<bool, CancellationToken, Task>)scenario.Database.Access.SetBrowserAuditAsync;
        var originalLocal = await scenario.SnapshotAsync(native: false);
        ExceptionDispatchInfo? primary = null;
        try
        {
            await access(false, TestContext.Current.CancellationToken);
            using var response = await scenario.Browser.GetAsync(callback, TestContext.Current.CancellationToken);
            BrowserLoginTests.RequireDenied(response, HttpStatusCode.ServiceUnavailable);
            Assert.True(scenario.Database.Access.IsDenied(observation.Failure));
            await BrowserCallbackTests.RequireNoTicketAsync(scenario, sends: 1);
            Assert.Equal(originalLocal, await scenario.SnapshotAsync(native: false));
        }
        catch (Exception error) { primary = ExceptionDispatchInfo.Capture(error); }
        using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(CleanupSeconds));
        try
        {
            await access(true, cleanup.Token);
        }
        catch (Exception error) when (primary is not null) { throw new AggregateException(primary.SourceException, error); }
        primary?.Throw();
        var before = await scenario.SnapshotAsync();
        using var replay = await BrowserCallbackTests.ReplayAsync(scenario.Host, callback, cookies);
        BrowserLoginTests.RequireDenied(replay, HttpStatusCode.ServiceUnavailable);
        Assert.Equal(before, await scenario.SnapshotAsync());
        var next = await scenario.CallbackAsync();
        using var accepted = await scenario.Browser.GetAsync(next, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.SeeOther, accepted.StatusCode);
        Assert.Equal(2, scenario.Host.Bridge.TokenSends);
    }

    /// <summary>Discovers independent browser and canonical-ticket audit denial for both required providers.</summary>
    public static IEnumerable<TheoryDataRow<string, bool>> Variants => from provider in ProviderNames
                                                                       from ticket in TicketVariants
                                                                       select new TheoryDataRow<string, bool>(provider, ticket).WithTrait(ProviderTrait, provider);
}
