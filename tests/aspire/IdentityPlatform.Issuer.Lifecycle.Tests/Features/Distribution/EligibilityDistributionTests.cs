using System.Net;
using System.Text.Json;
using IdentityPlatform.AuthServer.Features.Pilot;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using IdentityPlatform.Issuer.Lifecycle.Tests.Fixtures;
using IdentityPlatform.Issuer.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Issuer.Lifecycle.Tests.Features.Distribution;

[Collection("distribution")]
public sealed class EligibilityDistributionTests(DistributionFixture fixture)
{
    private static readonly string[] Restrictions = ["account", "session", "client", "scope"];
    public static IEnumerable<TheoryDataRow<string, string, string, string>> Cases =>
        from provider in DistributionEvidence.Providers
        from source in DistributionEvidence.Nodes
        from restriction in Restrictions
        select new TheoryDataRow<string, string, string, string>(provider, source, DistributionEvidence.Other(source), restriction)
            .WithTrait("Provider", provider);

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Other_warm_node_enforces_committed_restrictions_at_authorization_and_exchange(
        string provider, string source, string target, string restriction)
    {
        var environment = await fixture.GetAsync(provider);
        var cookies = new CookieContainer();
        using var browser = environment.NodeClient(source, cookies, autoRedirect: true);
        using var transport = environment.NodeClient(target);
        var warm = await environment.AuthorizeAsync(browser);
        using var warmTokens = await DistributionEvidence.RedeemAsync(environment, source, warm);
        await DistributionEvidence.SilentAsync(environment, target, cookies, warm);
        var evidence = await ExchangeEvidence.IssueAsync(environment, browser);
        await using var scope = environment.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var applications = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        var scopes = scope.ServiceProvider.GetRequiredService<IOpenIddictScopeManager>();
        var user = await database.Users.SingleAsync(value => value.Id == evidence.Subject, TestContext.Current.CancellationToken);
        var session = await database.Set<LocalIdentitySession>().SingleAsync(value => value.Id == evidence.SessionId, TestContext.Current.CancellationToken);
        var application = (await applications.FindByClientIdAsync(PilotProfile.ClientId, TestContext.Current.CancellationToken))!;
        var apiScope = (await scopes.FindByNameAsync(PilotProfile.Scope, TestContext.Current.CancellationToken))!;
        var originalApplication = new OpenIddictApplicationDescriptor();
        var originalScope = new OpenIddictScopeDescriptor();
        await applications.PopulateAsync(originalApplication, application, TestContext.Current.CancellationToken);
        await scopes.PopulateAsync(originalScope, apiScope, TestContext.Current.CancellationToken);
        try
        {
            if (restriction == "account")
            {
                user.IsEnabled = false;
                await database.SaveChangesAsync(TestContext.Current.CancellationToken);
            }
            else if (restriction == "session")
            {
                session.Revoked = true;
                await database.SaveChangesAsync(TestContext.Current.CancellationToken);
            }
            else if (restriction == "client")
            {
                var changed = new OpenIddictApplicationDescriptor();
                await applications.PopulateAsync(changed, application, TestContext.Current.CancellationToken);
                changed.Properties["enabled"] = JsonSerializer.SerializeToElement(false);
                await applications.UpdateAsync(application, changed, TestContext.Current.CancellationToken);
            }
            else
            {
                var changed = new OpenIddictScopeDescriptor();
                await scopes.PopulateAsync(changed, apiScope, TestContext.Current.CancellationToken);
                changed.Properties["enabled"] = JsonSerializer.SerializeToElement(false);
                await scopes.UpdateAsync(apiScope, changed, TestContext.Current.CancellationToken);
            }
            var authorizationState = await RestartEvidence.StateAsync(environment, includeRequestTokens: false);
            await DistributionEvidence.SilentAsync(environment, target, cookies, evidence.Flow,
                restriction is "account" or "session" ? "login_required" : "access_denied");
            Assert.True(authorizationState == await RestartEvidence.StateAsync(environment, includeRequestTokens: false),
                "Denied authorization changed session, interaction, issuance or audit state.");
            var before = await ExchangeSnapshot.CaptureAsync(environment);
            using var denied = await environment.RelyingParty.ExchangeAsync(evidence.Flow.Code, evidence.Flow.Verifier,
                transport: transport, cancellationToken: TestContext.Current.CancellationToken);
            await evidence.AssertDeniedAsync(environment, denied, before);
            var code = await ExchangeEvidence.Tokens(database).SingleAsync(value => value.Id == evidence.CodeId, TestContext.Current.CancellationToken);
            Assert.True(code.ExpirationDate > DateTime.UtcNow.AddSeconds(10) && code.RedemptionDate is null,
                "Eligibility denial must not be an expired or consumed code.");
        }
        finally
        {
            using var restoration = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            try
            {
                if (restriction == "account")
                {
                    user.IsEnabled = true;
                    await database.SaveChangesAsync(restoration.Token);
                }
                if (restriction == "client")
                {
                    await applications.UpdateAsync(application, originalApplication, restoration.Token);
                }
                if (restriction == "scope")
                {
                    await scopes.UpdateAsync(apiScope, originalScope, restoration.Token);
                }
                // Revoked sessions stay revoked; recovery requires a genuinely new login.
            }
            catch
            {
                environment.Invalidate();
            }
        }
        Assert.False(environment.Invalidated, "Could not restore eligibility configuration.");
        using var recovery = environment.NodeClient(target, autoRedirect: true);
        var fresh = await environment.AuthorizeAsync(recovery);
        using var tokens = await DistributionEvidence.RedeemAsync(environment, source, fresh);
    }
}
