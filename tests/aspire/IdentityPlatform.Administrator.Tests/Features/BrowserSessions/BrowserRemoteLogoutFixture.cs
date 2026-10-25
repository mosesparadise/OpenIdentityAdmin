using System.Net;
using System.Text.RegularExpressions;
using IdentityPlatform.Admin.Bff.Features.BrowserSessions;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using IdentityPlatform.Infrastructure.Features.Sessions.Browser;
using IdentityPlatform.Infrastructure.Features.Sessions.Persistence;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Connects the actual BFF-produced correlation to genuine native issuer confirmation without inventing callback state.</summary>
internal sealed class BrowserRemoteLogoutFixture(IssuerLogoutFixture issuer) : IAsyncDisposable
{
    private const int LinkFieldCount = 4;
    private const int PendingRevision = 2;
    private const int ReturnedRevision = 3;

    private const string IssuerChoice = "issuer";
    private const string ClientId = "identity-admin-bff";
    private const string ReturnPath = "/bff/logout/return";
    private const string LocalSuccessText = "session has ended locally";
    private const string UnconfirmedText = "Local sign-out could not be confirmed";
    private const string ScriptElement = "<script";
    private const string IssuerLinkId = "issuer-sign-out";
    private const string LinkPattern = "id=\"issuer-sign-out\" href=\"([^\"]+)\"";
    private const string Choice = "Mode";
    internal IssuerLogoutFixture Issuer { get; } = issuer;
    internal BrowserLoginFixture Login => Issuer.Login;
    private string? correlation;
    internal Uri Return { get; private set; } = null!;

    /// <summary>Owns the real fresh login, canonical callback and issuer end-session host using separate browser jars.</summary>
    internal static async Task<BrowserRemoteLogoutFixture> CreateAsync(BootstrapFixture fixture, string provider) =>
        new(await IssuerLogoutFixture.CreateAsync(fixture, provider));

    /// <summary>Posts the exact protected assisted choice; no issuer request occurs as a side effect of this call.</summary>
    internal async Task<HttpResponseMessage> PostAsync(HttpClient? browser = null, string mode = IssuerChoice)
    {
        browser ??= Login.Browser;
        var token = await BrowserLogoutHttp.EntryAsync(browser);
        return await BrowserIssuerHttp.PostAsync(browser, BrowserLogoutHttp.Path,
            new Dictionary<string, string> { [AdministratorJourney.Antiforgery] = token, [Choice] = mode });
    }

    /// <summary>Requires fixed four-field navigation built by product code and retains all sensitive material only in memory.</summary>
    internal async Task BeginAsync(HttpClient? browser = null)
    {
        using var response = await PostAsync(browser);
        await RequireLocalAsync(response);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var match = Regex.Match(html, LinkPattern, RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        Assert.True(match.Success);
        var link = new Uri(WebUtility.HtmlDecode(match.Groups[1].Value));
        Assert.True(link.GetLeftPart(UriPartial.Path) == new Uri(Login.Profile.Issuer, IssuerLogoutFixture.Path).AbsoluteUri);
        var query = QueryHelpers.ParseQuery(link.Query);
        Assert.True(query.Count == LinkFieldCount && query.All(pair => pair.Value.Count == 1));
        Assert.True(query[IssuerLogoutFixture.Client][0] == ClientId && query[IssuerLogoutFixture.Hint][0] == Issuer.Source);
        Assert.True(query[IssuerLogoutFixture.Return][0] == new Uri(Login.Profile.ClientUri, ReturnPath).AbsoluteUri);
        Assert.NotNull(BrowserLoginSelector.Digest(query[IssuerLogoutFixture.State][0]));
        Issuer.Fields.Clear();
        foreach (var pair in query)
        {
            Issuer.Fields.Add(pair.Key, pair.Value[0]!);
        }
        correlation = BrowserLoginSelector.Digest(Issuer.Fields[IssuerLogoutFixture.State]);
        Return = new(QueryHelpers.AddQueryString(Issuer.Fields[IssuerLogoutFixture.Return], IssuerLogoutFixture.State, Issuer.Fields[IssuerLogoutFixture.State]));
        Assert.NotNull(Login.Cookies.GetCookies(Login.Profile.ClientUri)[BrowserSessionCookies.Logout]);
        await RequireContinuationAsync(BrowserLogoutState.RemotePending);
    }

    /// <summary>Confirms only local commit, finite safe headers and absent automatic redirect or retry.</summary>
    internal static async Task RequireLocalAsync(HttpResponseMessage response, bool link = true)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        BrowserLogoutHttp.RequirePageHeaders(response);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.True(html.Contains(LocalSuccessText, StringComparison.Ordinal));
        Assert.False(html.Contains(UnconfirmedText, StringComparison.Ordinal));
        Assert.False(html.Contains(ScriptElement, StringComparison.Ordinal));
        Assert.Null(response.Headers.Location);
        if (!link)
        {
            Assert.False(html.Contains(IssuerLinkId, StringComparison.Ordinal));
        }
    }

    /// <summary>Reads exact persisted correlation provenance and unique local/remote audit effects independently of navigation text.</summary>
    internal Task<BrowserLogoutContinuation> RequireContinuationAsync(BrowserLogoutState state) => Login.ReadAsync(async db =>
    {
        var row = await db.Set<BrowserLogoutContinuation>().AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
        var browser = await db.Set<BrowserContext>().AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
        var ticket = await db.Set<AdminSessionTicket>().AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
        Assert.True(row.BrowserDigest == browser.Digest && row.Generation == browser.Generation && row.CorrelationId == browser.CorrelationId);
        Assert.True(row.Subject == ticket.Subject && row.SourceSession == ticket.SourceSession && row.SourceSession == Issuer.Source);
        Assert.Equal(state, row.State);
        if (correlation is not null)
        {
            Assert.True(row.Digest == correlation);
        }
        Assert.Equal(state == BrowserLogoutState.LocalCommitted ? 1 : state == BrowserLogoutState.RemotePending ? PendingRevision : ReturnedRevision, row.Revision);
        var audits = await db.Set<BrowserSessionAudit>().AsNoTracking().Where(item => item.Generation == row.Generation).ToArrayAsync(TestContext.Current.CancellationToken);
        Assert.True(audits.All(item => item.CorrelationId == row.CorrelationId));
        var local = audits.Single(item => item.Operation == BrowserMutation.Logout);
        Assert.True(local.Subject == ticket.Subject && local.SourceSession == ticket.SourceSession);
        Assert.Equal(state == BrowserLogoutState.LocalCommitted ? 0 : 1, audits.Count(item => item.Operation == BrowserMutation.RemotePending));
        Assert.Equal(state == BrowserLogoutState.ReturnedUnverified ? 1 : 0, audits.Count(item => item.Operation == BrowserMutation.Return));
        var revoked = await db.Set<AdminSessionAudit>().AsNoTracking().SingleAsync(item => item.Operation == SessionMutation.Revoke, TestContext.Current.CancellationToken);
        Assert.True(revoked.Digest == ticket.Digest && revoked.Revision == ticket.Revision);
        Assert.Equal(SessionMutationOutcome.Committed, revoked.Outcome);
        Assert.Null(browser.TicketDigest);
        Assert.NotNull(ticket.RevokedAt);
        Assert.Equal(1, await db.Set<BrowserSessionAudit>().CountAsync(item => item.Operation == BrowserMutation.Logout, TestContext.Current.CancellationToken));
        Assert.Equal(1, await db.Set<AdminSessionAudit>().CountAsync(item => item.Operation == SessionMutation.Revoke, TestContext.Current.CancellationToken));
        return row;
    });

    /// <summary>Releases native host consumers before real database and synthetic key ownership.</summary>
    public ValueTask DisposeAsync() => Issuer.DisposeAsync();
}
