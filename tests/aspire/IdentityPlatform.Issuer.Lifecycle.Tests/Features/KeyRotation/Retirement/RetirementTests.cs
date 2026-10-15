using System.Net;
using IdentityPlatform.Issuer.Lifecycle.Tests.Fixtures;
using IdentityPlatform.Issuer.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace IdentityPlatform.Issuer.Lifecycle.Tests.Features.KeyRotation.Retirement;

[Collection("distribution")]
public sealed class RetirementTests
{
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Actual_expiry_and_all_old_generation_termination_precede_two_node_retirement(string provider)
    {
        await using var fixture = await KeyRotationEnvironment.CreateAsync(provider);
        var host = fixture.Host;
        foreach (var node in DistributionEvidence.Nodes)
        {
            await fixture.ChangeAsync(node, KeyStage.Staged);
        }
        var cookies = new CookieContainer();
        using var browser = host.NodeClient("auth", cookies, autoRedirect: true);
        var original = await host.AuthorizeAsync(browser);
        using var tokens = await DistributionEvidence.RedeemAsync(host, "auth", original);
        var frozen = await KeyEvidence.PublicKeysAsync(host, "auth", fixture.Original.Signing.Thumbprint, fixture.Next.Signing.Thumbprint);
        var accessExpiry = new DateTimeOffset(new JsonWebToken(tokens.RootElement.GetProperty("access_token").GetString()!).ValidTo);
        var identityExpiry = new DateTimeOffset(new JsonWebToken(tokens.RootElement.GetProperty("id_token").GetString()!).ValidTo);
        var request = await CachedRequestEvidence.CreateAsync(host, "auth");
        var requestExpiry = await KeyEvidence.RequirePayloadAsync(fixture, request.Reference, TokenTypeIdentifiers.Private.RequestToken,
            fixture.Original.Signing.Thumbprint, fixture.Original.Encryption.Thumbprint);
        var code = await ExchangeEvidence.IssueAsync(host, browser);
        var codeExpiry = await KeyEvidence.RequirePayloadAsync(fixture, code.Flow.Code, TokenTypeIdentifiers.Private.AuthorizationCode,
            fixture.Original.Signing.Thumbprint, fixture.Original.Encryption.Thumbprint);
        await fixture.ChangeAsync("auth-secondary", KeyStage.Active);
        Assert.False(fixture.Retirement.ConfirmEnded(host.CurrentNodeGenerations(), DateTimeOffset.UtcNow, ProcessTreeEvidence.Terminated));
        await fixture.ChangeAsync("auth", KeyStage.Active);
        Assert.True(fixture.Retirement.ConfirmEnded(host.CurrentNodeGenerations(), DateTimeOffset.UtcNow, ProcessTreeEvidence.Terminated));
        Assert.False(fixture.Retirement.CanRetire(host.CurrentNodeGenerations(), DateTimeOffset.UtcNow));
        await WaitBeyondAsync(codeExpiry);
        using (var transport = host.NodeClient("auth-secondary"))
        {
            var before = await ExchangeSnapshot.CaptureAsync(host);
            using var denied = await host.RelyingParty.ExchangeAsync(code.Flow.Code, code.Flow.Verifier, transport: transport,
                cancellationToken: TestContext.Current.CancellationToken);
            await code.AssertDeniedAsync(host, denied, before);
            await DistributionEvidence.NativeErrorAsync(denied, "ID2016");
        }
        await WaitBeyondAsync(new[] { fixture.Retirement.Earliest, accessExpiry, identityExpiry, requestExpiry }.Max());
        Assert.True(fixture.Retirement.CanRetire(host.CurrentNodeGenerations(), DateTimeOffset.UtcNow,
            codeExpiry, requestExpiry, accessExpiry, identityExpiry));
        await request.RequireDeniedAsync(host, "auth-secondary", "ID2019");
        Assert.Equal("Independent JWT validation failed: SecurityTokenExpiredException, SecurityTokenExpiredException",
            await host.RelyingParty.ValidateAsync(tokens.RootElement, original, TestContext.Current.CancellationToken, frozenJwks: frozen));
        await RequireOriginalExpiryAsync(host, code.Flow.Code, codeExpiry);
        await RequireOriginalExpiryAsync(host, request.Reference, requestExpiry);
        foreach (var node in DistributionEvidence.Nodes)
        {
            await fixture.ChangeAsync(node, KeyStage.Retired);
            await KeyEvidence.PublicKeysAsync(host, node, fixture.Next.Signing.Thumbprint);
            await DistributionEvidence.SilentAsync(host, node, cookies, original);
            using var fresh = host.NodeClient(node, autoRedirect: true);
            var flow = await host.AuthorizeAsync(fresh);
            await KeyEvidence.RequirePayloadAsync(fixture, flow.Code, TokenTypeIdentifiers.Private.AuthorizationCode,
                fixture.Next.Signing.Thumbprint, fixture.Next.Encryption.Thumbprint);
            using var issued = await DistributionEvidence.RedeemAsync(host, node, flow);
            await KeyEvidence.RequirePublicAsync(fixture, node, flow, issued.RootElement, fixture.Next.Signing.Thumbprint);
        }
    }

    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public void Incomplete_stale_live_boundary_and_rollback_evidence_cannot_authorize_retirement(string provider)
    {
        Assert.Contains(provider, DistributionEvidence.Providers);
        var clock = new ElapsedClock();
        var window = new RetirementWindow(clock);
        var now = DateTimeOffset.UtcNow;
        var oldA = new ProcessGeneration(1, 1);
        var oldB = new ProcessGeneration(2, 1);
        var current = new Dictionary<string, ProcessGeneration> { ["auth"] = new(3, 1), ["auth-secondary"] = new(4, 1) };
        Assert.False(window.CanRetire(current, now));
        Assert.False(window.ConfirmEnded(current, now, _ => true));
        window.ObserveOld("auth", oldA);
        Assert.False(window.ConfirmEnded(current, now, _ => true));
        window.ObserveOld("auth-secondary", oldB);
        Assert.False(window.ConfirmEnded(current, now, _ => false));
        Assert.False(window.ConfirmEnded(new Dictionary<string, ProcessGeneration> { ["auth"] = oldA, ["auth-secondary"] = oldB }, now, _ => true));
        Assert.True(window.ConfirmEnded(current, now, _ => true));
        Assert.False(window.CanRetire(current, now.AddDays(1))); // A wall-clock jump cannot advance elapsed time.
        clock.Seconds = 300;
        Assert.False(window.CanRetire(current, now.AddSeconds(301)));
        clock.Seconds = 301;
        Assert.False(window.CanRetire(current, now.AddSeconds(300)));
        Assert.False(window.CanRetire(current, now.AddSeconds(301), now.AddSeconds(302)));
        Assert.True(window.CanRetire(current, now.AddSeconds(301)));
        var unknown = new Dictionary<string, ProcessGeneration>(current) { ["auth"] = new(5, 1) };
        Assert.False(window.CanRetire(unknown, now.AddSeconds(301)));
        window.ObserveOld("auth", current["auth"]); // Rolling back this live generation invalidates the old boundary.
        Assert.False(window.CanRetire(current, now.AddSeconds(600)));
        Assert.False(window.ConfirmEnded(current, now.AddSeconds(400), _ => true));
        clock.Seconds = 400;
        Assert.True(window.ConfirmEnded(unknown, now.AddSeconds(400), _ => true));
        clock.Seconds = 700;
        Assert.False(window.CanRetire(unknown, now.AddSeconds(700)));
        Assert.False(window.CanRetire(unknown, now.AddSeconds(701)));
        clock.Seconds = 701;
        Assert.True(window.CanRetire(unknown, now.AddSeconds(701)));
    }

    private sealed class ElapsedClock : TimeProvider
    {
        public long Seconds
        {
            get; set;
        }
        public override long TimestampFrequency => 1;
        public override long GetTimestamp() => Seconds;
    }

    private static async Task WaitBeyondAsync(DateTimeOffset boundary)
    {
        var remaining = boundary.AddMilliseconds(500) - DateTimeOffset.UtcNow;
        Assert.True(remaining < TimeSpan.FromSeconds(301), "Retirement wait exceeds the unchanged pilot lifetime.");
        if (remaining > TimeSpan.Zero)
        {
            await Task.Delay(remaining, TestContext.Current.CancellationToken);
        }
        Assert.True(DateTimeOffset.UtcNow > boundary);
    }

    private static async Task RequireOriginalExpiryAsync(IssuerEnvironment host, string reference, DateTimeOffset expected)
    {
        await using var scope = host.Services.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictTokenManager>();
        var entry = await manager.FindByReferenceIdAsync(reference, TestContext.Current.CancellationToken);
        Assert.NotNull(entry);
        Assert.True(await manager.GetExpirationDateAsync(entry, TestContext.Current.CancellationToken) == expected &&
            await manager.GetRedemptionDateAsync(entry, TestContext.Current.CancellationToken) is null, "Expiry control changed or redeemed the original artifact.");
    }
}
