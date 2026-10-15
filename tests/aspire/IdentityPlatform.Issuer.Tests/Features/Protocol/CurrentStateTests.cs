using System.Net;
using System.Text.Json;
using IdentityPlatform.AuthServer.Features.Pilot;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using IdentityPlatform.Issuer.Tests.Fixtures;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Issuer.Tests.Features.Protocol;

[Collection("issuer")]
public sealed class CurrentStateTests(IssuerFixture fixture)
{
    private static readonly string[] Providers = ["PostgreSql", "SqlServer"];
    private static readonly string[] Mutations = ["disabled", "lockout", "lockout-disabled", "two-factor", "stamp", "session-revoked", "session-expired", "client-disabled", "native-client", "client-lifetime", "permission", "pkce-requirement", "scope-disabled", "scope-resource"];
    public static IEnumerable<TheoryDataRow<string, string>> Cases =>
        from provider in Providers
        from mutation in Mutations
        select new TheoryDataRow<string, string>(provider, mutation).WithTrait("Provider", provider)
            .WithTrait("CriticalDecision", mutation switch
            {
                "disabled" => "account-eligibility",
                "session-revoked" => "session-eligibility",
                _ => "none"
            });

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Exchange_rechecks_current_state_even_without_scope_parameter(string provider, string mutation)
    {
        var environment = await fixture.GetAsync(provider);
        using var browser = IssuerEnvironment.CreateBrowserClient();
        var flow = await environment.AuthorizeAsync(browser);
        await using var scope = environment.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<PlatformUser>>();
        var applications = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        var scopes = scope.ServiceProvider.GetRequiredService<IOpenIddictScopeManager>();
        var user = (await users.FindByNameAsync(environment.Username))!;
        var originalUser = database.Entry(user).CurrentValues.Clone();
        var sessions = await database.Set<LocalIdentitySession>().Where(value => value.UserId == user.Id && !value.Revoked).ToArrayAsync(TestContext.Current.CancellationToken);
        var originalSessions = sessions.Select(value => database.Entry(value).CurrentValues.Clone()).ToArray();
        var application = (await applications.FindByClientIdAsync(PilotProfile.ClientId, TestContext.Current.CancellationToken))!;
        var applicationOriginal = new OpenIddictApplicationDescriptor();
        await applications.PopulateAsync(applicationOriginal, application, TestContext.Current.CancellationToken);
        var appChange = new OpenIddictApplicationDescriptor();
        await applications.PopulateAsync(appChange, application, TestContext.Current.CancellationToken);
        var apiScope = (await scopes.FindByNameAsync(PilotProfile.Scope, TestContext.Current.CancellationToken))!;
        var scopeOriginal = new OpenIddictScopeDescriptor();
        await scopes.PopulateAsync(scopeOriginal, apiScope, TestContext.Current.CancellationToken);
        var scopeChange = new OpenIddictScopeDescriptor();
        await scopes.PopulateAsync(scopeChange, apiScope, TestContext.Current.CancellationToken);
        try
        {
            switch (mutation)
            {
                case "disabled":
                    user.IsEnabled = false;
                    break;
                case "lockout":
                    user.LockoutEnd = DateTimeOffset.UtcNow.AddMinutes(5);
                    break;
                case "lockout-disabled":
                    user.LockoutEnabled = false;
                    break;
                case "two-factor":
                    user.TwoFactorEnabled = true;
                    break;
                case "stamp":
                    user.SecurityStamp = Guid.NewGuid().ToString("N");
                    break;
                case "session-revoked":
                    foreach (var session in sessions)
                    {
                        session.Revoked = true;
                    }
                    break;
                case "session-expired":
                    foreach (var session in sessions)
                    {
                        session.ExpiresAt = 1;
                    }
                    break;
                case "client-disabled":
                    appChange.Properties["enabled"] = JsonSerializer.SerializeToElement(false);
                    break;
                case "native-client":
                    appChange.ApplicationType = OpenIddictConstants.ApplicationTypes.Native;
                    break;
                case "client-lifetime":
                    appChange.Settings[OpenIddictConstants.Settings.TokenLifetimes.AccessToken] = "01:00:00";
                    break;
                case "permission":
                    appChange.Permissions.Remove(OpenIddictConstants.Permissions.Prefixes.Scope + PilotProfile.Scope);
                    break;
                case "pkce-requirement":
                    appChange.Requirements.Clear();
                    break;
                case "scope-disabled":
                    scopeChange.Properties["enabled"] = JsonSerializer.SerializeToElement(false);
                    break;
                case "scope-resource":
                    scopeChange.Resources.Clear();
                    scopeChange.Resources.Add("unexpected-api");
                    break;
                default:
                    throw new InvalidOperationException("Unknown mutation.");
            }
            await database.SaveChangesAsync(TestContext.Current.CancellationToken);
            await applications.UpdateAsync(application, appChange, TestContext.Current.CancellationToken);
            await scopes.UpdateAsync(apiScope, scopeChange, TestContext.Current.CancellationToken);
            using var response = await environment.RelyingParty.ExchangeAsync(flow.Code, flow.Verifier, cancellationToken: TestContext.Current.CancellationToken);
            if (mutation is "disabled" or "session-revoked")
            {
                await CriticalDecisionAssertions.DeniedTokensAsync(response,
                    async tokens => string.IsNullOrEmpty(await environment.RelyingParty.ValidateAsync(tokens, flow, TestContext.Current.CancellationToken)),
                    mutation == "disabled" ? "Critical decision bypass: account-eligibility." : "Critical decision bypass: session-eligibility.");
            }
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
            Assert.True(body.RootElement.TryGetProperty("error", out _));
            Assert.False(body.RootElement.TryGetProperty("access_token", out _));
            Assert.False(body.RootElement.TryGetProperty("id_token", out _));
        }
        finally
        {
            using var restoration = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            try
            {
                database.Entry(user).CurrentValues.SetValues(originalUser);
                for (var index = 0; index < sessions.Length; index++)
                {
                    database.Entry(sessions[index]).CurrentValues.SetValues(originalSessions[index]);
                }
                await database.SaveChangesAsync(restoration.Token);
                await applications.UpdateAsync(application, applicationOriginal, restoration.Token);
                await scopes.UpdateAsync(apiScope, scopeOriginal, restoration.Token);
            }
            catch
            {
                environment.Invalidate();
            }
            Assert.False(environment.Invalidated, "Baseline state could not be restored.");
        }
    }
}
