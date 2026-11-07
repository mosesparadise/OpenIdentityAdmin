using System.Net;
using System.Text.Json;
using IdentityPlatform.Administrator.Tests.Features.BrowserSessions;
using IdentityPlatform.Administrator.Tests.Features.ManagedApplications;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Persistence;
using IdentityPlatform.Issuer.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;

/// <summary>Retains exact independently persisted code bindings and changes one fixture authority through a separately committed context.</summary>
internal sealed class PilotWaitState(string codeId, string authorizationId, string applicationId, string subject, string sessionId, DateTimeOffset expiration)
{
    private const string Enabled = "enabled";
    private const string InvalidAuthority = "Unknown pilot wait authority.";
    private const string Cookie = "Set-Cookie";
    internal string CodeId => codeId;
    internal DateTimeOffset Expiration => expiration;

    /// <summary>Resolves the actual opaque code through the native manager and verifies its original subject/session and valid lifecycle.</summary>
    internal static async Task<PilotWaitState> ReadAsync(BrowserHttpsFixture environment, TestRelyingParty client, TestFlow flow)
    {
        await using var scope = Observer(environment);
        var token = TestContext.Current.CancellationToken;
        var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictTokenManager>();
        var code = await manager.FindByReferenceIdAsync(flow.Code, token);
        Assert.NotNull(code);
        Assert.True(await manager.HasTypeAsync(code, OpenIddictConstants.TokenTypeIdentifiers.Private.AuthorizationCode, token));
        Assert.True(await manager.HasStatusAsync(code, OpenIddictConstants.Statuses.Valid, token));
        var expected = await client.ExpectedIdentity(flow, token);
        Assert.True(await manager.GetSubjectAsync(code, token) == expected.Subject);
        return new((await manager.GetIdAsync(code, token))!, (await manager.GetAuthorizationIdAsync(code, token))!,
            (await manager.GetApplicationIdAsync(code, token))!, expected.Subject, flow.ExpectedSessionId!,
            (await manager.GetExpirationDateAsync(code, token))!.Value);
    }

    /// <summary>Commits a single restriction while the original HTTP request is independently proved blocked on the separate gate.</summary>
    internal async Task RestrictAsync(BrowserHttpsFixture environment, string authority)
    {
        await using var scope = Observer(environment);
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var token = TestContext.Current.CancellationToken;
        await using var transaction = await database.Database.BeginTransactionAsync(token);
        switch (authority)
        {
            case PilotWaitTests.Account:
                Assert.Equal(1, await database.Users.Where(row => row.Id == subject && row.IsEnabled)
                    .ExecuteUpdateAsync(update => update.SetProperty(row => row.IsEnabled, false), token));
                break;
            case PilotWaitTests.Session:
                Assert.Equal(1, await database.Set<LocalIdentitySession>().Where(row => row.Id == sessionId && !row.Revoked)
                    .ExecuteUpdateAsync(update => update.SetProperty(row => row.Revoked, true), token));
                break;
            case PilotWaitTests.Authorization:
                var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictAuthorizationManager>();
                var authorization = await manager.FindByIdAsync(authorizationId, token);
                Assert.NotNull(authorization);
                Assert.True(await manager.TryRevokeAsync(authorization, token));
                break;
            case PilotWaitTests.Application:
            case PilotWaitTests.Scope:
                await RegistrationAsync(scope.ServiceProvider, authority, enabled: false);
                break;
            default:
                throw new InvalidOperationException(InvalidAuthority);
        }
        await transaction.CommitAsync(token);
    }

    /// <summary>Restores only reversible account/profile fixture restrictions; revoked original sessions and authorizations stay revoked.</summary>
    internal async Task RestoreAsync(BrowserHttpsFixture environment, string authority)
    {
        await using var scope = Observer(environment);
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var token = TestContext.Current.CancellationToken;
        await using var transaction = await database.Database.BeginTransactionAsync(token);
        if (authority == PilotWaitTests.Account)
        {
            Assert.Equal(1, await database.Users.Where(row => row.Id == subject && !row.IsEnabled)
                .ExecuteUpdateAsync(update => update.SetProperty(row => row.IsEnabled, true), token));
        }
        else if (authority is PilotWaitTests.Application or PilotWaitTests.Scope)
        {
            await RegistrationAsync(scope.ServiceProvider, authority, enabled: true);
        }
        await transaction.CommitAsync(token);
    }

    /// <summary>Uses native descriptor/update APIs and exact manifest IDs, preserving every other configuration field.</summary>
    private async Task RegistrationAsync(IServiceProvider services, string authority, bool enabled)
    {
        var token = TestContext.Current.CancellationToken;
        if (authority == PilotWaitTests.Application)
        {
            var manager = services.GetRequiredService<IOpenIddictApplicationManager>();
            var application = await manager.FindByIdAsync(applicationId, token);
            Assert.NotNull(application);
            var descriptor = new OpenIddictApplicationDescriptor();
            await manager.PopulateAsync(descriptor, application, token);
            Assert.Equal(!enabled, descriptor.Properties[Enabled].GetBoolean());
            descriptor.Properties[Enabled] = JsonSerializer.SerializeToElement(enabled);
            await manager.UpdateAsync(application, descriptor, token);
        }
        else
        {
            var manifest = await services.GetRequiredService<PlatformDbContext>().Set<PilotManifest>().AsNoTracking()
                .SingleAsync(row => row.ApplicationId == applicationId, token);
            var manager = services.GetRequiredService<IOpenIddictScopeManager>();
            var scope = await manager.FindByIdAsync(manifest.ScopeId, token);
            Assert.NotNull(scope);
            var descriptor = new OpenIddictScopeDescriptor();
            await manager.PopulateAsync(descriptor, scope, token);
            Assert.Equal(!enabled, descriptor.Properties[Enabled].GetBoolean());
            descriptor.Properties[Enabled] = JsonSerializer.SerializeToElement(enabled);
            await manager.UpdateAsync(scope, descriptor, token);
        }
    }

    /// <summary>Hashes all actual native token/authorization rows without exposing protected payloads in failure output.</summary>
    internal static async Task<string> SnapshotAsync(BrowserHttpsFixture environment)
    {
        await using var scope = Observer(environment);
        return await ManagedJourneyEvidence.NativeAsync(scope.ServiceProvider.GetRequiredService<PlatformDbContext>());
    }

    /// <summary>Requires a new genuine password/code exchange and independent public-key validation after reversible authority is restored.</summary>
    internal static async Task PositiveAsync(BrowserHttpsFixture environment, TestRelyingParty client)
    {
        var flow = await PilotReplayJourney.AuthorizeAsync(environment, client);
        using var response = await client.ExchangeAsync(flow.Code, flow.Verifier, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await client.ValidateAsync(body.RootElement, flow, TestContext.Current.CancellationToken));
    }

    /// <summary>Requires finite unavailable output without protocol credentials, cookies or redirects.</summary>
    internal static async Task UnavailableAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Null(response.Headers.Location);
        Assert.False(response.Headers.Contains(Cookie));
        Assert.True(response.Headers.CacheControl?.NoStore == true);
        Assert.Empty(await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>Retains the native invalid_grant oracle and independently excludes every successful credential and browser authority channel.</summary>
    internal static async Task RejectedAsync(HttpResponseMessage response)
    {
        await InitialPilotReplayEvidence.RequireRejectedAsync(response);
        Assert.Null(response.Headers.Location);
        Assert.False(response.Headers.Contains(Cookie));
        Assert.True(response.Headers.CacheControl?.NoStore == true);
        using var body = JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
        Assert.False(body.RootElement.TryGetProperty(OpenIddictConstants.Parameters.IdToken, out _));
        Assert.False(body.RootElement.TryGetProperty(OpenIddictConstants.Parameters.RefreshToken, out _));
    }

    /// <summary>Selects legacy native storage before resolving any manager, while retaining standalone composition without adding selection services.</summary>
    internal static AsyncServiceScope Observer(BrowserHttpsFixture environment)
    {
        var scope = environment.IssuerServices.CreateAsyncScope();
        scope.ServiceProvider.GetService<ProtocolStorageSelection>()?.Select(ProtocolStorageMode.LegacyPilot);
        return scope;
    }
}
