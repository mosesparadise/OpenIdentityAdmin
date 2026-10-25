using System.Net;
using IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Identity.Journeys;
using IdentityPlatform.Infrastructure.Features.Sessions.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Verifies native registration, browser framing and already-revoked source boundaries without weakening the positive flow.</summary>
[Collection("administrator-bootstrap")]
public sealed class IssuerLogoutBoundaryTests(BootstrapFixture fixture)
{
    private const string ForeignClient = "foreign-client";
    private const string ForeignReturn = "https://foreign.example.test/";
    private const string TokenHint = "id_token_hint";
    private const string InvalidHint = "not-a-token";

    /// <summary>Native credential hints, wrong fixed metadata, duplicate parameters and absent cookies cannot revoke any source.</summary>
    [Theory, MemberData(nameof(BrowserLoginTests.Providers), MemberType = typeof(BrowserLoginTests))]
    public async Task Given_ValidSource_When_HostileNativeFramingOrMissingCookieIsSubmitted_Then_DenialLeavesOriginalConfirmationUsable(string provider)
    {
        await using var scenario = await IssuerLogoutFixture.CreateAsync(fixture, provider);
        var form = await scenario.FormAsync();
        var variants = new[]
        {
            new Dictionary<string, string>(scenario.Fields) { [IssuerLogoutFixture.Client] = ForeignClient },
            new Dictionary<string, string>(scenario.Fields) { [IssuerLogoutFixture.Return] = ForeignReturn },
            new Dictionary<string, string>(scenario.Fields) { [IssuerLogoutFixture.Hint] = scenario.Login.Issuer.Account.Grant.SessionId },
            new Dictionary<string, string>(scenario.Fields) { [TokenHint] = InvalidHint }
        };
        foreach (var values in variants)
        {
            using var denied = await scenario.GetAsync(values);
            IssuerLogoutTests.RequireDenied(denied);
        }
        using var anonymous = scenario.Host.Browser();
        using var missing = await scenario.GetAsync(browser: anonymous);
        IssuerLogoutTests.RequireDenied(missing);
        using var duplicate = await BrowserIssuerHttp.PostAsync(scenario.Browser, IssuerLogoutFixture.Path,
            form.Append(KeyValuePair.Create(IssuerLogoutFixture.Hint, scenario.Source)));
        IssuerLogoutTests.RequireDenied(duplicate);
        var effects = await scenario.EffectsAsync();
        Assert.True(effects.Sessions.Values.All(revoked => !revoked));
        Assert.Equal(0, effects.Audits);
        using var control = await scenario.PostAsync(form);
        await scenario.RequireReturnAsync(control);
        await scenario.RequireRevokedAsync();
    }

    /// <summary>Current profile disablement after GET denies POST without treating earlier native validation as continuing authority.</summary>
    [Theory, MemberData(nameof(BrowserLoginTests.Providers), MemberType = typeof(BrowserLoginTests))]
    public async Task Given_ValidConfirmation_When_ProfileIsDisabled_Then_NoIssuerSessionIsRevoked(string provider)
    {
        await using var scenario = await IssuerLogoutFixture.CreateAsync(fixture, provider);
        var form = await scenario.FormAsync();
        await NativeRegistrationDrift.ApplyAsync(scenario.Login.Issuer, NativeRegistrationDrift.Change.Disabled);
        using var response = await scenario.PostAsync(form);
        IssuerLogoutTests.RequireDenied(response);
        var effects = await scenario.EffectsAsync();
        Assert.True(effects.Sessions.Values.All(revoked => !revoked));
        Assert.Equal(0, effects.Audits);
    }

    /// <summary>Another real serialized logout wins first; submitting the retained confirmation cannot create a second audit or return.</summary>
    [Theory, MemberData(nameof(BrowserLoginTests.Providers), MemberType = typeof(BrowserLoginTests))]
    public async Task Given_ValidConfirmation_When_SourceRevocationCommitsFirst_Then_RetainedCookieCannotAuthorizeAnotherLogout(string provider)
    {
        await using var scenario = await IssuerLogoutFixture.CreateAsync(fixture, provider);
        var form = await scenario.FormAsync();
        var subject = await scenario.Login.ReadAsync(database => database.Set<AdminSessionTicket>().Select(row => row.Subject).SingleAsync(TestContext.Current.CancellationToken));
        await using (var scope = scenario.Login.Issuer.Account.Journey.CreateScope())
        {
            Assert.True(await scope.ServiceProvider.GetRequiredService<JourneyLogout>().RevokeAsync(new(subject, scenario.Source), TestContext.Current.CancellationToken));
        }
        using var response = await scenario.PostAsync(form);
        IssuerLogoutTests.RequireDenied(response);
        await scenario.RequireRevokedAsync();
    }

    /// <summary>Two genuinely protected requests race on the actual provider; at most one native return and exactly one audit can succeed.</summary>
    [Theory, MemberData(nameof(BrowserLoginTests.Providers), MemberType = typeof(BrowserLoginTests))]
    public async Task Given_TwoProtectedSubmissions_When_TheyRace_Then_OnlyOneSourceRevocationCommits(string provider)
    {
        await using var scenario = await IssuerLogoutFixture.CreateAsync(fixture, provider);
        var form = await scenario.FormAsync();
        var responses = await Task.WhenAll(scenario.PostAsync(form), scenario.PostAsync(form));
        try
        {
            var successes = responses.Count(response => response.StatusCode == HttpStatusCode.Redirect);
            Assert.Equal(1, successes);
            await scenario.RequireReturnAsync(responses.First(response => response.StatusCode == HttpStatusCode.Redirect));
            IssuerLogoutTests.RequireDenied(responses.First(response => response.StatusCode != HttpStatusCode.Redirect));
            await scenario.RequireRevokedAsync();
        }
        finally { foreach (var response in responses) { response.Dispose(); } }
    }
}
