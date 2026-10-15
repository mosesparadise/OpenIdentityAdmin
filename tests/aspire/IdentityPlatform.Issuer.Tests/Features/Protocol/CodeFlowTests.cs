using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using IdentityPlatform.Issuer.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Issuer.Tests.Features.Protocol;

[Collection("issuer")]
public sealed class CodeFlowTests(IssuerFixture fixture)
{
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Independent_validator_rejects_wrong_issuer_audience_type_signature_and_nonce(string provider)
    {
        var environment = await fixture.GetAsync(provider);
        using var browser = IssuerEnvironment.CreateBrowserClient();
        var flow = await environment.AuthorizeAsync(browser);
        using var response = await environment.RelyingParty.ExchangeAsync(flow.Code, flow.Verifier, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var tokens = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await environment.RelyingParty.ValidateAsync(tokens.RootElement, flow, TestContext.Current.CancellationToken));
        var keys = new JsonWebKeySet(await browser.GetStringAsync(new Uri(environment.Issuer, "/connect/jwks"), TestContext.Current.CancellationToken)).GetSigningKeys();
        var handler = new JsonWebTokenHandler();
        var encoded = tokens.RootElement.GetProperty("id_token").GetString()!;
        using var unrelated = RSA.Create(2048);
        // Each independent negative retains the other valid expectations and uses public keys only.
        var invalid = new[]
        {
            TestRelyingParty.Parameters("https://different-issuer.invalid/", "identityplatform-pilot", "JWT", keys),
            TestRelyingParty.Parameters(environment.Issuer.AbsoluteUri, "different-client", "JWT", keys),
            TestRelyingParty.Parameters(environment.Issuer.AbsoluteUri, "identityplatform-pilot", "at+jwt", keys),
            TestRelyingParty.Parameters(environment.Issuer.AbsoluteUri, "identityplatform-pilot", "JWT", [new RsaSecurityKey(unrelated.ExportParameters(false))])
        };
        foreach (var parameters in invalid)
        {
            Assert.False((await handler.ValidateTokenAsync(encoded, parameters)).IsValid);
        }
        var otherFlow = new TestFlow { ExpectedSessionId = flow.ExpectedSessionId };
        Assert.Equal("Nonce mismatch.", await environment.RelyingParty.ValidateAsync(tokens.RootElement, otherFlow, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Native_code_expires_after_the_actual_sixty_second_lifetime(string provider)
    {
        var environment = await fixture.GetAsync(provider);
        using var browser = IssuerEnvironment.CreateBrowserClient();
        var flow = await environment.AuthorizeAsync(browser);
        await Task.Delay(TimeSpan.FromSeconds(61), TestContext.Current.CancellationToken);
        using var response = await environment.RelyingParty.ExchangeAsync(flow.Code, flow.Verifier, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal("invalid_grant", body.RootElement.GetProperty("error").GetString());
        Assert.False(body.RootElement.TryGetProperty("access_token", out _));
    }

    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Real_form_callback_and_independent_token_validation_succeed(string provider)
    {
        var environment = await fixture.GetAsync(provider);
        using var browser = IssuerEnvironment.CreateBrowserClient();
        var flow = await environment.AuthorizeAsync(browser, exchange: true);
        Assert.True(flow.Validated);
        Assert.Empty(flow.Error);
    }

    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    [Trait("CriticalDecision", "native-replay")]
    public async Task Concurrent_valid_redemption_has_exactly_one_winner_and_replay_fails(string provider)
    {
        var environment = await fixture.GetAsync(provider);
        using var browser = IssuerEnvironment.CreateBrowserClient();
        var flow = await environment.AuthorizeAsync(browser);
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<HttpResponseMessage> Exchange()
        {
            await start.Task;
            return await environment.RelyingParty.ExchangeAsync(flow.Code, flow.Verifier, cancellationToken: TestContext.Current.CancellationToken);
        }
        var first = Exchange();
        var second = Exchange();
        start.SetResult();
        var responses = await Task.WhenAll(first, second);
        using var one = responses[0];
        using var two = responses[1];
        await CriticalDecisionAssertions.SingleWinnerAsync(responses,
            async tokens => string.IsNullOrEmpty(await environment.RelyingParty.ValidateAsync(tokens, flow, TestContext.Current.CancellationToken)),
            "Critical decision bypass: native-replay.");
        var winner = responses.Single(response => response.StatusCode == HttpStatusCode.OK);
        var loser = responses.Single(response => response.StatusCode == HttpStatusCode.BadRequest);
        using var lost = JsonDocument.Parse(await loser.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal("invalid_grant", lost.RootElement.GetProperty("error").GetString());
        using var tokens = JsonDocument.Parse(await winner.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await environment.RelyingParty.ValidateAsync(tokens.RootElement, flow, TestContext.Current.CancellationToken));
        using var replay = await environment.RelyingParty.ExchangeAsync(flow.Code, flow.Verifier, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, replay.StatusCode);
        using var error = JsonDocument.Parse(await replay.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal("invalid_grant", error.RootElement.GetProperty("error").GetString());
        var access = new JsonWebToken(tokens.RootElement.GetProperty("access_token").GetString()!);
        await using var scope = environment.Services.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictTokenManager>();
        var persisted = await manager.FindByIdAsync(access.GetClaim("oi_tkn_id").Value, TestContext.Current.CancellationToken);
        Assert.NotNull(persisted);
        Assert.True(await manager.HasStatusAsync(persisted, OpenIddictConstants.Statuses.Revoked, TestContext.Current.CancellationToken));
    }
}
