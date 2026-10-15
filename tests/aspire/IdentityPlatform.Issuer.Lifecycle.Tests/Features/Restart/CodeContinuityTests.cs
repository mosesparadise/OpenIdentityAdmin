using System.Net;
using System.Text.Json;
using IdentityPlatform.Infrastructure.Persistence;
using IdentityPlatform.Issuer.Lifecycle.Tests.Fixtures;
using IdentityPlatform.Issuer.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Issuer.Lifecycle.Tests.Features.Restart;

[Collection("lifecycle")]
public sealed class CodeContinuityTests(LifecycleFixture fixture)
{
    private static readonly string[] Providers = ["PostgreSql", "SqlServer"];
    private static readonly string[] BindingKinds = ["proof", "client", "redirect"];

    public static IEnumerable<TheoryDataRow<string, string>> Bindings =>
        from provider in Providers
        from binding in BindingKinds
        select new TheoryDataRow<string, string>(provider, binding).WithTrait("Provider", provider);

    [Theory]
    [MemberData(nameof(Bindings))]
    public async Task Exact_live_code_retains_bindings_and_redeems_after_restart(string provider, string binding)
    {
        var environment = await fixture.GetAsync(provider);
        using var browser = IssuerEnvironment.CreateBrowserClient();
        if (binding == "client")
        {
            await using var scope = environment.Services.CreateAsyncScope();
            var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
            var registered = new OpenIddictApplicationDescriptor
            {
                ClientId = "lifecycle-other-client",
                ClientSecret = environment.ClientSecret,
                ClientType = OpenIddictConstants.ClientTypes.Confidential,
                Permissions = { OpenIddictConstants.Permissions.Endpoints.Token, OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode },
                RedirectUris = { new Uri(environment.RelyingParty.Callback) }
            };
            await manager.CreateAsync(registered, TestContext.Current.CancellationToken);
        }
        var evidence = await ExchangeEvidence.IssueAsync(environment, browser);
        await environment.RestartIssuerAsync();
        await evidence.RequireFreshAsync(environment);
        var before = await ExchangeSnapshot.CaptureAsync(environment);
        using (var denied = await environment.RelyingParty.ExchangeAsync(evidence.Flow.Code,
            binding == "proof" ? new string('x', 43) : evidence.Flow.Verifier,
            clientId: binding == "client" ? "lifecycle-other-client" : "identityplatform-pilot",
            redirect: binding == "redirect" ? environment.RelyingParty.Callback + "/changed" : null,
            cancellationToken: TestContext.Current.CancellationToken))
        {
            await evidence.AssertDeniedAsync(environment, denied, before);
            await RequireNativeErrorAsync(denied, binding == "client" ? "ID2069" : binding == "redirect" ? "ID2072" : "ID2052");
        }
        await evidence.RequireFreshAsync(environment);
        using var accepted = await RedeemAsync(environment, evidence);
        await RequireRedeemedAsync(environment, evidence);
        before = await ExchangeSnapshot.CaptureAsync(environment);
        using var replay = await environment.RelyingParty.ExchangeAsync(evidence.Flow.Code, evidence.Flow.Verifier, cancellationToken: TestContext.Current.CancellationToken);
        await evidence.AssertDeniedAsync(environment, replay, before);
    }

    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Still_unexpired_redeemed_code_remains_replayed_and_revokes_linked_tokens(string provider)
    {
        var environment = await fixture.GetAsync(provider);
        using var browser = IssuerEnvironment.CreateBrowserClient();
        var evidence = await ExchangeEvidence.IssueAsync(environment, browser);
        using var accepted = await RedeemAsync(environment, evidence);
        var access = new JsonWebToken(accepted.RootElement.GetProperty("access_token").GetString()!);
        var entryId = access.GetClaim("oi_tkn_id").Value;
        await environment.RestartIssuerAsync();
        await RequireRedeemedAsync(environment, evidence);
        var before = await ExchangeSnapshot.CaptureAsync(environment);
        using var replay = await environment.RelyingParty.ExchangeAsync(evidence.Flow.Code, evidence.Flow.Verifier, cancellationToken: TestContext.Current.CancellationToken);
        await evidence.AssertDeniedAsync(environment, replay, before);
        using var body = JsonDocument.Parse(await replay.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.True(body.RootElement.GetProperty("error_uri").GetString()?.EndsWith("ID2010", StringComparison.Ordinal) == true,
            "Replay must reach the native already-redeemed decision, not an expiry denial.");
        await using var scope = environment.Services.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictTokenManager>();
        var entry = await manager.FindByIdAsync(entryId, TestContext.Current.CancellationToken);
        Assert.True(entry is not null && await manager.HasStatusAsync(entry, OpenIddictConstants.Statuses.Revoked, TestContext.Current.CancellationToken),
            "Native replay did not revoke the linked issued token.");
    }

    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Original_code_reaches_unchanged_real_expiry_and_fresh_flow_recovers(string provider)
    {
        var environment = await fixture.GetAsync(provider);
        using var browser = IssuerEnvironment.CreateBrowserClient();
        var evidence = await ExchangeEvidence.IssueAsync(environment, browser);
        DateTime expiry;
        await using (var scope = environment.Services.CreateAsyncScope())
        {
            var row = await ExchangeEvidence.Tokens(scope.ServiceProvider.GetRequiredService<PlatformDbContext>())
                .SingleAsync(value => value.Id == evidence.CodeId, TestContext.Current.CancellationToken);
            expiry = row.ExpirationDate!.Value;
            Assert.True(expiry - row.CreationDate <= TimeSpan.FromSeconds(60), "Native code lifetime was relaxed.");
        }
        await environment.RestartIssuerAsync();
        await evidence.RequireFreshAsync(environment);
        var remaining = expiry.AddSeconds(1) - DateTime.UtcNow;
        Assert.True(remaining > TimeSpan.Zero && remaining < TimeSpan.FromSeconds(60));
        await Task.Delay(remaining, TestContext.Current.CancellationToken);
        var before = await ExchangeSnapshot.CaptureAsync(environment);
        using var denied = await environment.RelyingParty.ExchangeAsync(evidence.Flow.Code, evidence.Flow.Verifier, cancellationToken: TestContext.Current.CancellationToken);
        await evidence.AssertDeniedAsync(environment, denied, before);
        await RequireNativeErrorAsync(denied, "ID2016");
        await using (var scope = environment.Services.CreateAsyncScope())
        {
            var row = await ExchangeEvidence.Tokens(scope.ServiceProvider.GetRequiredService<PlatformDbContext>())
                .SingleAsync(value => value.Id == evidence.CodeId, TestContext.Current.CancellationToken);
            Assert.True(row.ExpirationDate == expiry && row.RedemptionDate is null, "Expiry control changed or redeemed the original code.");
        }
        using var recovery = IssuerEnvironment.CreateBrowserClient();
        Assert.True((await environment.AuthorizeAsync(recovery, exchange: true)).Validated);
    }

    private static async Task RequireNativeErrorAsync(HttpResponseMessage response, string identifier)
    {
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.True(body.RootElement.TryGetProperty("error_uri", out var uri) && uri.ValueKind == JsonValueKind.String &&
            uri.GetString() == "https://documentation.openiddict.com/errors/" + identifier,
            "Denial did not reach the expected native protocol decision.");
    }

    internal static async Task<JsonDocument> RedeemAsync(IssuerEnvironment environment, ExchangeEvidence evidence)
    {
        using var response = await environment.RelyingParty.ExchangeAsync(evidence.Flow.Code, evidence.Flow.Verifier, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var tokens = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var failure = await environment.RelyingParty.ValidateAsync(tokens.RootElement, evidence.Flow, TestContext.Current.CancellationToken);
        Assert.True(failure.Length == 0, "Independent public token validation failed.");
        SafeAssertions.ContainsNoSensitiveMaterial(failure, evidence.Flow.Code, evidence.Flow.Verifier, environment.Password, environment.ClientSecret);
        return tokens;
    }

    private static async Task RequireRedeemedAsync(IssuerEnvironment environment, ExchangeEvidence evidence)
    {
        await using var scope = environment.Services.CreateAsyncScope();
        var row = await ExchangeEvidence.Tokens(scope.ServiceProvider.GetRequiredService<PlatformDbContext>())
            .SingleAsync(value => value.Id == evidence.CodeId, TestContext.Current.CancellationToken);
        Assert.True(row.Status == OpenIddictConstants.Statuses.Redeemed && row.RedemptionDate is not null && row.ExpirationDate > DateTime.UtcNow.AddSeconds(10),
            "The exact redeemed code must remain comfortably unexpired for the replay control.");
    }
}
