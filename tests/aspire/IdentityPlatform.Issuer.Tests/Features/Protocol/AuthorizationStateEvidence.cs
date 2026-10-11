using System.Text.Json;
using IdentityPlatform.AuthServer.Features.Pilot;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using IdentityPlatform.Issuer.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Issuer.Tests.Features.Protocol;

public sealed partial class AuthorizationStateTests
{
    private sealed class RestrictionState(IssuerEnvironment environment, LoginAccountState account,
        OpenIddictApplicationDescriptor application, OpenIddictScopeDescriptor scope) : IAsyncDisposable
    {
        private bool applicationChanged;
        private bool scopeChanged;

        public static async Task<RestrictionState> CaptureAsync(IssuerEnvironment environment)
        {
            var account = await LoginAccountState.CaptureAsync(environment);
            await using var inspection = environment.Services.CreateAsyncScope();
            var applications = inspection.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
            var scopes = inspection.ServiceProvider.GetRequiredService<IOpenIddictScopeManager>();
            var application = new OpenIddictApplicationDescriptor();
            var apiScope = new OpenIddictScopeDescriptor();
            await applications.PopulateAsync(application, (await applications.FindByClientIdAsync(PilotProfile.ClientId, TestContext.Current.CancellationToken))!, TestContext.Current.CancellationToken);
            await scopes.PopulateAsync(apiScope, (await scopes.FindByNameAsync(PilotProfile.Scope, TestContext.Current.CancellationToken))!, TestContext.Current.CancellationToken);
            return new(environment, account, application, apiScope);
        }

        public async Task CommitAsync(string restriction, string sessionId)
        {
            await using var mutation = environment.Services.CreateAsyncScope();
            var database = mutation.ServiceProvider.GetRequiredService<PlatformDbContext>();
            var session = await database.Set<LocalIdentitySession>().SingleAsync(value => value.Id == sessionId, TestContext.Current.CancellationToken);
            var user = await database.Users.SingleAsync(value => value.Id == session.UserId, TestContext.Current.CancellationToken);
            switch (restriction)
            {
                case "disabled":
                    user.IsEnabled = false;
                    break;
                case "stamp":
                    user.SecurityStamp = Guid.NewGuid().ToString("N");
                    break;
                case "session-revoked":
                    session.Revoked = true;
                    break;
                case "session-expired":
                    session.ExpiresAt = 1;
                    break;
                case "client-disabled":
                case "permission":
                    var applications = mutation.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
                    var client = (await applications.FindByClientIdAsync(PilotProfile.ClientId, TestContext.Current.CancellationToken))!;
                    var changedApplication = new OpenIddictApplicationDescriptor();
                    await applications.PopulateAsync(changedApplication, client, TestContext.Current.CancellationToken);
                    if (restriction == "client-disabled")
                    {
                        changedApplication.Properties["enabled"] = JsonSerializer.SerializeToElement(false);
                    }
                    else
                    {
                        Assert.True(changedApplication.Permissions.Remove(OpenIddictConstants.Permissions.Prefixes.Scope + PilotProfile.Scope));
                    }
                    applicationChanged = true;
                    await applications.UpdateAsync(client, changedApplication, TestContext.Current.CancellationToken);
                    break;
                case "scope-disabled":
                case "scope-resource":
                    var scopes = mutation.ServiceProvider.GetRequiredService<IOpenIddictScopeManager>();
                    var apiScope = (await scopes.FindByNameAsync(PilotProfile.Scope, TestContext.Current.CancellationToken))!;
                    var changedScope = new OpenIddictScopeDescriptor();
                    await scopes.PopulateAsync(changedScope, apiScope, TestContext.Current.CancellationToken);
                    if (restriction == "scope-disabled")
                    {
                        changedScope.Properties["enabled"] = JsonSerializer.SerializeToElement(false);
                    }
                    else
                    {
                        changedScope.Resources.Clear();
                        changedScope.Resources.Add("unexpected-api");
                    }
                    scopeChanged = true;
                    await scopes.UpdateAsync(apiScope, changedScope, TestContext.Current.CancellationToken);
                    break;
                default:
                    throw new InvalidOperationException("Unknown authorization restriction.");
            }
            await database.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        public async ValueTask DisposeAsync()
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            try
            {
                await using var restoration = environment.Services.CreateAsyncScope();
                if (applicationChanged)
                {
                    var applications = restoration.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
                    await applications.UpdateAsync((await applications.FindByClientIdAsync(PilotProfile.ClientId, deadline.Token))!, application, deadline.Token);
                }
                if (scopeChanged)
                {
                    var scopes = restoration.ServiceProvider.GetRequiredService<IOpenIddictScopeManager>();
                    await scopes.UpdateAsync((await scopes.FindByNameAsync(PilotProfile.Scope, deadline.Token))!, scope, deadline.Token);
                }
            }
            catch { environment.Invalidate(); }
            finally { await account.DisposeAsync(); }
            Assert.False(environment.Invalidated, "Authorization restriction cleanup failed.");
            // Case-owned revoked/expired sessions stay terminal; recovery creates a new session.
        }
    }

    private sealed record AuthorizationSnapshot(Dictionary<string, string> Tokens, Dictionary<string, string> Authorizations,
        Dictionary<string, string> Sessions, Dictionary<string, string> Interactions, Dictionary<Guid, string> Audits, int Approvals)
    {
        public static async Task<AuthorizationSnapshot> CaptureAsync(IssuerEnvironment environment)
        {
            await using var scope = environment.Services.CreateAsyncScope();
            var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            // Native request-cache token creation/redemption is not authenticated issuance.
            var tokens = await database.Set<OpenIddictEntityFrameworkCoreToken>().AsNoTracking()
                .Where(value => value.Type != OpenIddictConstants.TokenTypeIdentifiers.Private.RequestToken)
                .Select(value => new { value.Id, value.Type, value.Status, value.Subject, value.RedemptionDate, value.ExpirationDate })
                .ToArrayAsync(TestContext.Current.CancellationToken);
            var authorizations = await database.Set<OpenIddictEntityFrameworkCoreAuthorization>().AsNoTracking()
                .Select(value => new { value.Id, value.Subject, value.Status, value.Type, value.Scopes, value.Properties, value.ConcurrencyToken })
                .ToArrayAsync(TestContext.Current.CancellationToken);
            var sessions = await database.Set<LocalIdentitySession>().AsNoTracking().ToArrayAsync(TestContext.Current.CancellationToken);
            var interactions = await database.Set<LoginInteraction>().AsNoTracking().ToArrayAsync(TestContext.Current.CancellationToken);
            var audits = await database.Set<SecurityAuditIntent>().AsNoTracking().ToArrayAsync(TestContext.Current.CancellationToken);
            return new(tokens.ToDictionary(value => value.Id!, value => JsonSerializer.Serialize(value)), authorizations.ToDictionary(value => value.Id!, value => JsonSerializer.Serialize(value)),
                sessions.ToDictionary(value => value.Id, value => JsonSerializer.Serialize(value)), interactions.ToDictionary(value => value.Id, value => JsonSerializer.Serialize(value)),
                audits.ToDictionary(value => value.Id, value => JsonSerializer.Serialize(value)), audits.Count(value => value.Event == "authorization-decision"));
        }

        public void RequireDenied(AuthorizationSnapshot after, bool allowAdmission)
        {
            Assert.True(Same(Tokens, after.Tokens) && Same(Authorizations, after.Authorizations), "Denial must preserve native issuance entries without adding codes, tokens or authorizations.");
            Assert.True(Same(Sessions, after.Sessions), "Denial must not create, replace or change authenticated sessions.");
            Assert.True(Same(Audits, after.Audits), "Denial must preserve the complete committed audit history without an authorization approval.");
            Assert.True(Interactions.All(value => after.Interactions.TryGetValue(value.Key, out var current) && value.Value == current), "Denial must preserve every existing interaction and its exact completion binding.");
            Assert.Equal(Interactions.Count + (allowAdmission ? 1 : 0), after.Interactions.Count);
        }

        private static bool Same<TKey>(Dictionary<TKey, string> before, Dictionary<TKey, string> after) where TKey : notnull =>
            before.Count == after.Count && before.All(value => after.TryGetValue(value.Key, out var current) && value.Value == current);
    }
}
