using System.Net;
using System.Runtime.ExceptionServices;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Sessions.Browser;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Requires real provider audit permission denial at each remote phase, preserving local revocation and restoring permissions independently.</summary>
[Collection("administrator-bootstrap")]
public sealed class BrowserRemoteLogoutAuditTests(BootstrapFixture fixture)
{
    private const string ProviderTrait = "Provider";
    private static readonly bool[] Phases = [false, true];
    private const int CleanupSeconds = 10;

    /// <summary>Denied remote audit INSERT rolls back only that phase; an explicit valid return works once permission is restored.</summary>
    [Theory, MemberData(nameof(Cases))]
    public async Task Given_RemoteAuditInsertDenied_When_ContinuationOrReturnRuns_Then_LocalSuccessAndOriginalRemoteStateRemain(string provider, bool returned)
    {
        await using var scenario = await BrowserRemoteLogoutFixture.CreateAsync(fixture, provider);
        if (returned)
        {
            await scenario.BeginAsync();
        }
        var attempts = 0;
        var fault = new BrowserRemoteLogoutFault(returned ? BrowserMutation.Return : BrowserMutation.RemotePending)
        {
            BeforeSave = async () =>
            {
                attempts++;
                await scenario.Login.Database.Access.SetBrowserAuditAsync(false, TestContext.Current.CancellationToken);
            }
        };
        await using var peer = await scenario.Login.PeerAsync(fault.Configure);
        using var browser = peer.Browser(scenario.Login.Cookies);
        ExceptionDispatchInfo? primary = null;
        try
        {
            using var response = returned ? await browser.GetAsync(scenario.Return, TestContext.Current.CancellationToken) : await scenario.PostAsync(browser);
            if (returned)
            {
                BrowserLoginTests.RequireDenied(response, HttpStatusCode.ServiceUnavailable);
            }
            else
            {
                await BrowserRemoteLogoutFixture.RequireLocalAsync(response, link: false);
            }
            Assert.Equal(1, attempts);
            Assert.Equal(0, fault.Commits);
            Assert.True(scenario.Login.Database.Access.IsDenied(fault.Failure));
            await scenario.RequireContinuationAsync(returned ? BrowserLogoutState.RemotePending : BrowserLogoutState.LocalCommitted);
        }
        catch (Exception error) { primary = ExceptionDispatchInfo.Capture(error); }
        using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(CleanupSeconds));
        try
        {
            await scenario.Login.Database.Access.SetBrowserAuditAsync(true, cleanup.Token);
        }
        catch (Exception error) when (primary is not null) { throw new AggregateException(primary.SourceException, error); }
        primary?.Throw();
        if (returned)
        {
            using var control = await scenario.Login.Browser.GetAsync(scenario.Return, TestContext.Current.CancellationToken);
            await BrowserRemoteLogoutTests.RequireReturnedAsync(control);
        }
    }

    /// <summary>Discovers both distinct remote audit boundaries against both actual database providers.</summary>
    public static IEnumerable<TheoryDataRow<string, bool>> Cases => from provider in IssuerContinuationProviderFixture.Providers
                                                                    from returned in Phases
                                                                    select new TheoryDataRow<string, bool>(provider, returned).WithTrait(ProviderTrait, provider);
}
