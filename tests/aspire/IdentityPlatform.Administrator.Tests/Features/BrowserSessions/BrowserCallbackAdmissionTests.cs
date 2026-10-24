using System.Net;
using IdentityPlatform.Admin.Bff.Features.BrowserSessions;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Sessions.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.Client;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Preserves durable native consumption when product proofs or mandatory callback adapters cannot authorize establishment.</summary>
[Collection("administrator-bootstrap")]
public sealed class BrowserCallbackAdmissionTests(BootstrapFixture fixture)
{
    private const string Missing = "missing";
    private const string Duplicate = "duplicate";
    private const string Foreign = "foreign";
    private const string Replacement = "replacement";
    private const string Admission = "admission";
    private const string Identity = "identity";
    private const string ProviderTrait = "Provider";
    private const string CookieSeparator = "; ";
    private const string Assignment = "=";

    private static readonly string[] ProviderNames = ["PostgreSql", "SqlServer"];
    private static readonly string[] Failures = [Missing, Duplicate, Foreign, Replacement, Admission, Identity];

    /// <summary>Genuine correlation still reaches native redemption, while invalid product binding never creates a local session.</summary>
    [Theory, MemberData(nameof(Variants))]
    public async Task Given_ValidNativeCallback_When_ProductOwnershipIsUnavailable_Then_StateIsSpentWithoutSessionOrRetry(string provider, string fault)
    {
        await using var scenario = await BrowserLoginFixture.CreateAsync(fixture, provider);
        var callback = await scenario.CallbackAsync();
        var originalCookies = scenario.Cookies.GetCookieHeader(callback);
        var cookies = await CookiesAsync(scenario, originalCookies, fault);
        await using var peer = await scenario.PeerAsync(services => services.Configure<OpenIddictClientOptions>(options =>
        {
            if (fault is Admission or Identity)
            {
                var type = fault == Admission ? typeof(BrowserCallbackAdmissionHandler) : typeof(BrowserCallbackIdentityHandler);
                options.Handlers.RemoveAll(handler => handler.ServiceDescriptor.ServiceType == type);
            }
        }));
        using var response = await BrowserCallbackTests.ReplayAsync(peer, callback, cookies);
        BrowserLoginTests.RequireDenied(response, HttpStatusCode.ServiceUnavailable);
        Assert.Equal(fault == Identity ? 1 : 0, peer.Bridge.TokenSends);
        Assert.Empty(await scenario.ReadAsync(db => db.Set<AdminSessionTicket>().ToArrayAsync(TestContext.Current.CancellationToken)));
        Assert.Equal(1, await scenario.ReadAsync(db => db.Set<OpenIddictEntityFrameworkCoreToken>()
            .CountAsync(row => row.Status == OpenIddictConstants.Statuses.Redeemed, TestContext.Current.CancellationToken)));
        var before = await scenario.SnapshotAsync();
        using var replay = await BrowserCallbackTests.ReplayAsync(scenario.Host, callback, originalCookies);
        BrowserLoginTests.RequireDenied(replay, HttpStatusCode.ServiceUnavailable);
        Assert.Equal(0, scenario.Host.Bridge.TokenSends);
        Assert.Equal(before, await scenario.SnapshotAsync());
    }

    /// <summary>Builds adversarial product cookies while retaining the exact original native correlation cookie.</summary>
    private static async Task<string> CookiesAsync(BrowserLoginFixture scenario, string original, string fault)
    {
        var cookies = original.Split(';', StringSplitOptions.TrimEntries).ToList();
        var pending = cookies.Single(value => value.StartsWith(BrowserSessionCookies.Prelogin + Assignment, StringComparison.Ordinal));
        if (fault == Missing)
        {
            cookies.Remove(pending);
        }
        if (fault == Duplicate)
        {
            cookies.Add(pending);
        }
        if (fault == Foreign)
        {
            var other = new CookieContainer();
            using var browser = scenario.Host.Browser(other);
            var intent = await scenario.StartAsync(await scenario.EntryAsync(browser), browser);
            var completion = await scenario.CompleteAsync(intent);
            using var resume = await scenario.ResumeAsync(completion, browser);
            Assert.Equal(HttpStatusCode.Redirect, resume.StatusCode);
            cookies.RemoveAll(value => value.StartsWith(BrowserSessionCookies.Generation + Assignment, StringComparison.Ordinal) ||
                value.StartsWith(BrowserSessionCookies.Prelogin + Assignment, StringComparison.Ordinal));
            cookies.AddRange(other.GetCookieHeader(scenario.Profile.ClientUri).Split(';', StringSplitOptions.TrimEntries).Where(value =>
                value.StartsWith(BrowserSessionCookies.Generation + Assignment, StringComparison.Ordinal) || value.StartsWith(BrowserSessionCookies.Prelogin + Assignment, StringComparison.Ordinal)));
        }
        if (fault == Replacement)
        {
            _ = await scenario.StartAsync(await scenario.EntryAsync());
        }
        return string.Join(CookieSeparator, cookies);
    }

    /// <summary>Discovers every mandatory adapter and browser-proof failure on both real providers.</summary>
    public static IEnumerable<TheoryDataRow<string, string>> Variants => from provider in ProviderNames
                                                                         from fault in Failures
                                                                         select new TheoryDataRow<string, string>(provider, fault).WithTrait(ProviderTrait, provider);
}
