using System.Net;
using System.Text.Json;
using IdentityPlatform.Infrastructure.Persistence;
using IdentityPlatform.Issuer.Lifecycle.Tests.Fixtures;
using IdentityPlatform.Issuer.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Issuer.Lifecycle.Tests.Features.Distribution;

[Collection("distribution")]
public sealed class CodeDistributionTests(DistributionFixture fixture)
{
    private static readonly string[] TokenNames = ["access_token", "id_token"];
    public static IEnumerable<TheoryDataRow<string, string, string>> Directions => DistributionEvidence.Directions;

    [Theory]
    [MemberData(nameof(Directions))]
    public async Task Exact_live_code_crosses_nodes_with_original_proof_and_single_use(string provider, string source, string target)
    {
        var environment = await fixture.GetAsync(provider);
        using var browser = environment.NodeClient(source, autoRedirect: true);
        using var transport = environment.NodeClient(target);
        var evidence = await ExchangeEvidence.IssueAsync(environment, browser);
        var before = await ExchangeSnapshot.CaptureAsync(environment);
        using (var denied = await environment.RelyingParty.ExchangeAsync(evidence.Flow.Code, new string('x', 43), transport: transport,
            cancellationToken: TestContext.Current.CancellationToken))
        {
            await evidence.AssertDeniedAsync(environment, denied, before);
            await DistributionEvidence.NativeErrorAsync(denied, "ID2052");
        }
        await evidence.RequireFreshAsync(environment);
        using var tokens = await DistributionEvidence.RedeemAsync(environment, target, evidence.Flow);
        before = await ExchangeSnapshot.CaptureAsync(environment);
        using var replay = await environment.RelyingParty.ExchangeAsync(evidence.Flow.Code, evidence.Flow.Verifier, transport: transport,
            cancellationToken: TestContext.Current.CancellationToken);
        await evidence.AssertDeniedAsync(environment, replay, before);
        await DistributionEvidence.NativeErrorAsync(replay, "ID2010");
        await RequireIssuedRevokedAsync(environment, tokens.RootElement);
    }

    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Concurrent_node_redemptions_have_one_native_winner_and_replay_revokes_issued_tokens(string provider)
    {
        var environment = await fixture.GetAsync(provider);
        using var browser = environment.NodeClient("auth", autoRedirect: true);
        var evidence = await ExchangeEvidence.IssueAsync(environment, browser);
        DateTime originalExpiration;
        await using (var scope = environment.Services.CreateAsyncScope())
        {
            var original = await ExchangeEvidence.Tokens(scope.ServiceProvider.GetRequiredService<PlatformDbContext>())
                .SingleAsync(value => value.Id == evidence.CodeId, TestContext.Current.CancellationToken);
            Assert.True(original.ExpirationDate is not null && original.ExpirationDate - original.CreationDate <= TimeSpan.FromSeconds(60));
            originalExpiration = original.ExpirationDate.Value;
        }
        using var primary = environment.NodeClient("auth");
        using var secondary = environment.NodeClient("auth-secondary");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var participants = 0;
        async Task<HttpResponseMessage> Attempt(HttpClient transport)
        {
            if (Interlocked.Increment(ref participants) == 2)
            {
                ready.SetResult();
            }
            await release.Task.WaitAsync(deadline.Token);
            return await environment.RelyingParty.ExchangeAsync(evidence.Flow.Code, evidence.Flow.Verifier,
                transport: transport, cancellationToken: deadline.Token);
        }
        var issuanceBefore = await ExchangeSnapshot.CaptureAsync(environment);
        var first = Attempt(primary);
        var second = Attempt(secondary);
        HttpResponseMessage[] responses;
        try
        {
            await ready.Task.WaitAsync(deadline.Token);
            Assert.False(first.IsCompleted || second.IsCompleted, "Redemptions escaped the rendezvous.");
            await evidence.RequireFreshAsync(environment);
            release.SetResult();
            responses = await Task.WhenAll(first, second);
        }
        catch
        {
            environment.Invalidate(); // Client cancellation does not prove native server redemption stopped.
            await deadline.CancelAsync();
            try
            {
                await Task.WhenAll(first, second);
            }
            catch (Exception) { /* Both owned attempts are drained; preserve the primary failure. */ }
            if (first.IsCompletedSuccessfully)
            {
                first.Result.Dispose();
            }
            if (second.IsCompletedSuccessfully)
            {
                second.Result.Dispose();
            }
            throw;
        }
        using var a = responses[0];
        using var b = responses[1];
        Assert.True(responses.Count(value => value.StatusCode == HttpStatusCode.OK) == 1 &&
            responses.Count(value => value.StatusCode == HttpStatusCode.BadRequest) == 1, "Concurrent redemption requires exactly one success and one native denial.");
        var winner = responses.Single(value => value.StatusCode == HttpStatusCode.OK);
        var loser = responses.Single(value => value.StatusCode == HttpStatusCode.BadRequest);
        await DistributionEvidence.NativeErrorAsync(loser, "ID2010");
        using var denial = JsonDocument.Parse(await loser.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.True(denial.RootElement.GetProperty("error").GetString() == "invalid_grant" &&
            !denial.RootElement.TryGetProperty("access_token", out _) && !denial.RootElement.TryGetProperty("id_token", out _));
        using var tokens = JsonDocument.Parse(await winner.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.True((await environment.RelyingParty.ValidateAsync(tokens.RootElement, evidence.Flow, TestContext.Current.CancellationToken, secondary)).Length == 0);
        var issuanceAfter = await ExchangeSnapshot.CaptureAsync(environment);
        var issuedIds = TokenNames.Select(name => new JsonWebToken(tokens.RootElement.GetProperty(name).GetString()!).GetClaim("oi_tkn_id").Value).ToHashSet(StringComparer.Ordinal);
        Assert.True(issuedIds.Count == 2 && issuanceAfter.TokenIds.Except(issuanceBefore.TokenIds).ToHashSet(StringComparer.Ordinal).SetEquals(issuedIds),
            "Concurrent attempts persisted issuance beyond the validated winner.");
        issuanceBefore.AssertAuditUnchanged(issuanceAfter);
        string expectedReplayError;
        await using (var scope = environment.Services.CreateAsyncScope())
        {
            var code = await ExchangeEvidence.Tokens(scope.ServiceProvider.GetRequiredService<PlatformDbContext>())
                .SingleAsync(value => value.Id == evidence.CodeId, TestContext.Current.CancellationToken);
            Assert.True(code.Status is OpenIddictConstants.Statuses.Redeemed or OpenIddictConstants.Statuses.Revoked &&
                code.RedemptionDate is not null && code.ExpirationDate == originalExpiration && originalExpiration > DateTime.UtcNow.AddSeconds(10),
                "Concurrent code outcome lost its native identity or lifetime.");
            expectedReplayError = code.Status == OpenIddictConstants.Statuses.Redeemed ? "ID2010" : "ID2016";
        }
        var before = await ExchangeSnapshot.CaptureAsync(environment);
        using var replay = await environment.RelyingParty.ExchangeAsync(evidence.Flow.Code, evidence.Flow.Verifier, transport: primary,
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(originalExpiration > DateTime.UtcNow, "Replay response arrived after the original code expired.");
        await evidence.AssertDeniedAsync(environment, replay, before);
        await DistributionEvidence.NativeErrorAsync(replay, expectedReplayError);
        await RequireIssuedRevokedAsync(environment, tokens.RootElement);
        using var recoveryBrowser = environment.NodeClient("auth-secondary", autoRedirect: true);
        var recovery = await environment.AuthorizeAsync(recoveryBrowser);
        using var recoveryTokens = await DistributionEvidence.RedeemAsync(environment, "auth", recovery);
    }

    private static async Task RequireIssuedRevokedAsync(IssuerEnvironment environment, JsonElement tokens)
    {
        await using var scope = environment.Services.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictTokenManager>();
        foreach (var name in TokenNames)
        {
            var id = new JsonWebToken(tokens.GetProperty(name).GetString()!).GetClaim("oi_tkn_id").Value;
            var entry = await manager.FindByIdAsync(id, TestContext.Current.CancellationToken);
            Assert.True(entry is not null && await manager.HasStatusAsync(entry, OpenIddictConstants.Statuses.Revoked, TestContext.Current.CancellationToken),
                "Native cross-node replay did not revoke the linked issued token.");
        }
    }
}
