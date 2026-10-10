using System.Net;
using System.Text.Json;
using IdentityPlatform.Issuer.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Issuer.Tests.Features.Protocol;

[Collection("issuer")]
public sealed class CodeFlowTests(IssuerFixture fixture)
{
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
        var winner = Assert.Single(responses, response => response.StatusCode == HttpStatusCode.OK);
        var loser = Assert.Single(responses, response => response.StatusCode == HttpStatusCode.BadRequest);
        using var lost = JsonDocument.Parse(await loser.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal("invalid_grant", lost.RootElement.GetProperty("error").GetString());
        using var tokens = JsonDocument.Parse(await winner.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await environment.RelyingParty.ValidateAsync(tokens.RootElement, flow.Nonce, TestContext.Current.CancellationToken));
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
