using System.Net;
using IdentityPlatform.Issuer.Lifecycle.Tests.Fixtures;
using IdentityPlatform.Issuer.Tests.Fixtures;

namespace IdentityPlatform.Issuer.Lifecycle.Tests.Features.Distribution;

[Collection("distribution")]
public sealed class ProtectionDistributionTests(DistributionFixture fixture)
{
    private static readonly string[] InvalidKinds = ["wrapper", "missing"];
    public static IEnumerable<TheoryDataRow<string, string>> InvalidMaterial =>
        from provider in DistributionEvidence.Providers
        from kind in InvalidKinds
        select new TheoryDataRow<string, string>(provider, kind).WithTrait("Provider", provider);

    [Theory]
    [MemberData(nameof(InvalidMaterial))]
    public async Task Unsuitable_secondary_material_fails_safely_while_original_cookie_survives(string provider, string kind)
    {
        var environment = await fixture.GetAsync(provider);
        var cookies = new CookieContainer();
        using var browser = environment.NodeClient("auth", cookies, autoRedirect: true);
        var original = await environment.AuthorizeAsync(browser);
        using var originalTokens = await DistributionEvidence.RedeemAsync(environment, "auth", original);
        var sessions = await RestartEvidence.SessionsAsync(environment);
        await environment.WithSecondaryCredentialAsync(kind, async () =>
        {
            await DistributionEvidence.SilentAsync(environment, "auth", cookies, original);
        });
        await DistributionEvidence.SilentAsync(environment, "auth-secondary", cookies, original);
        Assert.True(sessions == await RestartEvidence.SessionsAsync(environment), "Protection recovery replaced the original session.");
    }

    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Healthy_but_incompatible_encryption_denies_then_exact_original_code_recovers(string provider)
    {
        var environment = await fixture.GetAsync(provider);
        ExchangeEvidence? original = null;
        await environment.WithSecondaryCredentialAsync("encryption", async () =>
        {
            using var browser = environment.NodeClient("auth", autoRedirect: true);
            original = await ExchangeEvidence.IssueAsync(environment, browser);
            using var transport = environment.NodeClient("auth-secondary");
            var before = await RestartEvidence.StateAsync(environment);
            var exchange = await ExchangeSnapshot.CaptureAsync(environment);
            using var denied = await environment.RelyingParty.ExchangeAsync(original.Flow.Code, original.Flow.Verifier,
                transport: transport, cancellationToken: TestContext.Current.CancellationToken);
            await original.AssertDeniedAsync(environment, denied, exchange);
            await DistributionEvidence.NativeErrorAsync(denied, "ID2004");
            await RestartEvidence.RequireUnchangedAsync(environment, before);
            await original.RequireFreshAsync(environment);
        });
        Assert.NotNull(original);
        await original.RequireFreshAsync(environment);
        using var tokens = await DistributionEvidence.RedeemAsync(environment, "auth-secondary", original.Flow);
    }
}
