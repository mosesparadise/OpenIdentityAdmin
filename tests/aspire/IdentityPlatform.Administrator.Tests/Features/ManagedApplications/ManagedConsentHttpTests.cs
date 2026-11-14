using System.Net;
using System.Text.Json;
using IdentityPlatform.Administrator.Tests.Features.Applications;
using IdentityPlatform.Administrator.Tests.Fixtures;

namespace IdentityPlatform.Administrator.Tests.Features.ManagedApplications;

/// <summary>Verifies genuine consent, native tokens and current UserInfo with independent exact scope/audience/claim expectations on both providers.</summary>
[Collection("administrator-bootstrap")]
public sealed class ManagedConsentHttpTests(BootstrapFixture fixture)
{
    private const string ScopeField = "scope";
    private const string AccessField = "access_token";
    private const string EmailScope = "email";
    private const string EmailDescription = "Share your email";
    private const string OrdersDescription = "Read your orders";
    private const string EmailRequest = "email openid";

    /// <summary>Unrequested assigned scopes grant nothing; current consented email appears only in ID token/UserInfo and access audience follows requested API scope.</summary>
    [Theory]
    [InlineData("PostgreSql", "openid", false, Traits = ["Provider", "PostgreSql", "CriticalDecision", "managed-email-consent"])]
    [InlineData("PostgreSql", "email openid", false, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", "openid orders.read", true, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", "email openid orders.read", true, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", "openid", false, Traits = ["Provider", "SqlServer", "CriticalDecision", "managed-email-consent"])]
    [InlineData("SqlServer", "email openid", false, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", "openid orders.read", true, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", "email openid orders.read", true, Traits = ["Provider", "SqlServer"])]
    public async Task Given_AssignedScopes_When_UserConsentsToRequestedSubset_Then_NativeTokensAndCurrentUserInfoDiscloseOnlyThatSubset(string provider, string requested, bool verified)
    {
        await using var test = await ManagedCacheTestContext.CreateAsync(fixture, provider);
        await ManagedConsentTest.SeedAsync(test);
        await ManagedConsentTest.EmailAsync(test, ManagedConsentTest.Address, verified);
        var flow = new ManagedTokenFlow(test, requested);
        await flow.CacheAsync();
        await flow.DecideAsync(inspectConsent: html => Consent(html, requested));
        var code = await flow.CodeAsync();
        await ManagedConsentTest.EmailAsync(test, ManagedConsentTest.ChangedAddress, verified);
        using var exchange = await flow.ExchangeAsync(code);
        Assert.Equal(HttpStatusCode.OK, exchange.StatusCode);
        using var body = JsonDocument.Parse(await exchange.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
        Assert.True(body.RootElement.GetProperty(ScopeField).GetString() == requested);
        var row = Assert.Single((await test.SnapshotAsync()).Interactions);
        var email = requested.Contains(EmailScope, StringComparison.Ordinal) ? ManagedConsentTest.ChangedAddress : null;
        var resource = requested.Contains(ManagedConsentTest.ApiScope, StringComparison.Ordinal) ? ManagedConsentTest.Resource : null;
        await ManagedTokenOracle.ValidateAsync(test, flow, body.RootElement, row.UserId!, projection: new(requested, resource, email, email is null ? null : verified));
        await ManagedTokenEvidence.IssuanceAuditAsync(test);
        var access = body.RootElement.GetProperty(AccessField).GetString()!;
        await CurrentAsync(test, access, row.UserId!, email, verified);
        using var denied = await test.Http.DirectGetAsync(ApplicationHttpFixture.ApiCollection, credential: access);
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
    }

    /// <summary>Checks rendered plain-text consent against independently selected scope membership before the actual Allow submission.</summary>
    private static void Consent(string html, string requested)
    {
        Assert.Equal(requested.Contains(EmailScope, StringComparison.Ordinal), html.Contains(EmailDescription, StringComparison.Ordinal));
        Assert.Equal(requested.Contains(ManagedConsentTest.ApiScope, StringComparison.Ordinal), html.Contains(OrdersDescription, StringComparison.Ordinal));
        Assert.Equal(requested.Contains(ManagedConsentTest.ApiScope, StringComparison.Ordinal), html.Contains(ManagedConsentTest.Resource, StringComparison.Ordinal));
        Assert.DoesNotContain(ManagedConsentTest.Address, html, StringComparison.Ordinal);
    }

    /// <summary>Consent to email cannot fabricate either identity field when the current Identity account has no email at actual issuance.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_EmailConsentWithoutCurrentEmail_When_NativeExchangeRuns_Then_IdentityTokenAndUserInfoOmitBothFields(string provider)
    {
        await using var test = await ManagedCacheTestContext.CreateAsync(fixture, provider);
        await ManagedConsentTest.SeedAsync(test);
        await ManagedConsentTest.EmailAsync(test, null, false);
        var flow = new ManagedTokenFlow(test, EmailRequest);
        await flow.ApproveAsync();
        using var exchange = await flow.ExchangeAsync(await flow.CodeAsync());
        Assert.Equal(HttpStatusCode.OK, exchange.StatusCode);
        using var body = JsonDocument.Parse(await exchange.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
        var row = Assert.Single((await test.SnapshotAsync()).Interactions);
        await ManagedTokenOracle.ValidateAsync(test, flow, body.RootElement, row.UserId!, projection: new(EmailRequest));
        using var response = await ManagedConsentTest.ReadAsync(test, body.RootElement.GetProperty(AccessField).GetString()!);
        await ManagedConsentTest.RequireAsync(response, row.UserId!, null, false);
    }

    /// <summary>Exercises GET and both POST bearer transports, then checks current missing email without rewriting the immutable native token.</summary>
    private static async Task CurrentAsync(ManagedCacheTestContext test, string access, string subject, string? email, bool verified)
    {
        var before = await ManagedJourneyEvidence.DigestAsync(test);
        using var get = await ManagedConsentTest.ReadAsync(test, access);
        await ManagedConsentTest.RequireAsync(get, subject, email, verified);
        using var post = await ManagedConsentTest.ReadAsync(test, access, post: true);
        await ManagedConsentTest.RequireAsync(post, subject, email, verified);
        using var form = await ManagedConsentTest.ReadAsync(test, access, post: true, form: true);
        await ManagedConsentTest.RequireAsync(form, subject, email, verified);
        Assert.Equal(before, await ManagedJourneyEvidence.DigestAsync(test));
        await ManagedConsentTest.EmailAsync(test, null, false);
        using var missing = await ManagedConsentTest.ReadAsync(test, access);
        await ManagedConsentTest.RequireAsync(missing, subject, null, false);
    }
}
