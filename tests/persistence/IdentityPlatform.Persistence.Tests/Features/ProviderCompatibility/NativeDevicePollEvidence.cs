using System.Security.Claims;
using IdentityPlatform.Infrastructure.Features.Applications;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Persistence.Tests.Features.ProviderCompatibility;

/// <summary>Exercises machine-independent native public registration, native code lookup and committed cadence observations on a real provider.</summary>
internal static class NativeDevicePollEvidence
{
    private const int FirstSlowInterval = 10;
    private const string Client = "television-one";
    private const string Name = "Television";
    private const string UserCode = "synthetic-user-code";
    private const string Scope = "openid";
    private const string External = "synthetic-device-reference-for-polling";

    /// <summary>Polls through the native reference ID, reopens a fresh database scope and observes the persisted slowdown.</summary>
    internal static async Task RequireAsync(ManagedSchemaFixture fixture)
    {
        var factory = fixture.Services.GetRequiredService<IServiceScopeFactory>();
        var id = await SeedAsync(factory, fixture.Token);
        await using (var polling = await ProtocolStorageScope.OpenAsync(factory, fixture.Token))
        {
            Assert.Equal(DevicePollResult.SlowDown, await DevicePollAdmission.PollAsync(polling, Client, External, fixture.Token));
        }
        await using var scope = fixture.Scope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var policy = await database.Set<ApplicationPolicy>().SingleAsync(row => row.ApplicationId == id, fixture.Token);
        var persisted = await database.Set<DevicePollingWindow>().AsNoTracking().SingleAsync(row => row.ApplicationId == policy.ApplicationId, fixture.Token);
        Assert.Equal(FirstSlowInterval, persisted.IntervalSeconds);
        policy.Enabled = false;
        policy.Revision++;
        await database.SaveChangesAsync(fixture.Token);
        await using var disabled = await ProtocolStorageScope.OpenAsync(factory, fixture.Token);
        Assert.Equal(DevicePollResult.Unavailable, await DevicePollAdmission.PollAsync(disabled, Client, External, fixture.Token));
    }

    /// <summary>Seeds the library's real public application, inactive device entry and cadence row in one committed native transaction.</summary>
    private static async Task<string> SeedAsync(IServiceScopeFactory factory, CancellationToken token)
    {
        await using var scope = await ProtocolStorageScope.OpenAsync(factory, token);
        await using var transaction = await ProtocolStorageTransaction.OpenAsync(scope.Owner);
        var apps = scope.Services.GetRequiredService<IOpenIddictApplicationManager>();
        var app = await apps.CreateAsync(DeviceClientRegistration.Descriptor(Client, Name), token);
        var id = (await apps.GetIdAsync(app, token))!;
        var policy = new ApplicationPolicy { ApplicationId = id, Profile = DevicePollingWindow.Profile, Revision = 1 };
        await scope.Owner.RunAsync(async current =>
        {
            scope.Owner.Database.Add(policy);
            await scope.Owner.Database.SaveChangesAsync(current);
        }, token, write: true);
        await CreatePairAsync(scope, id, token);
        await transaction.CommitAsync(token);
        return id;
    }

    /// <summary>Links the native anonymous authorization and both native code records before retaining the polling interval.</summary>
    private static async Task CreatePairAsync(ProtocolStorageScope scope, string id, CancellationToken token)
    {
        var authorizations = scope.Services.GetRequiredService<IOpenIddictAuthorizationManager>();
        var authorization = await authorizations.CreateAsync(new OpenIddictAuthorizationDescriptor
        {
            ApplicationId = id,
            Type = OpenIddictConstants.AuthorizationTypes.AdHoc,
            Status = OpenIddictConstants.Statuses.Valid,
            Scopes = { Scope }
        }, token);
        var authId = (await authorizations.GetIdAsync(authorization, token))!;
        var tokens = scope.Services.GetRequiredService<IOpenIddictTokenManager>();
        var now = DateTimeOffset.FromUnixTimeSeconds(scope.Owner.Clock.GetUtcNow().ToUnixTimeSeconds());
        var device = await tokens.CreateAsync(new OpenIddictTokenDescriptor
        {
            ApplicationId = id,
            AuthorizationId = authId,
            CreationDate = now,
            ExpirationDate = now.AddSeconds(DevicePollingWindow.LifetimeSeconds),
            Status = OpenIddictConstants.Statuses.Inactive,
            Type = OpenIddictConstants.TokenTypeIdentifiers.Private.DeviceCode,
            ReferenceId = External
        }, token);
        var user = await tokens.CreateAsync(new OpenIddictTokenDescriptor
        {
            ApplicationId = id,
            AuthorizationId = authId,
            CreationDate = now,
            ExpirationDate = now.AddSeconds(DevicePollingWindow.LifetimeSeconds),
            Status = OpenIddictConstants.Statuses.Valid,
            Type = OpenIddictConstants.TokenTypeIdentifiers.Private.UserCode,
            ReferenceId = UserCode
        }, token);
        var principal = new ClaimsPrincipal(new ClaimsIdentity()).SetClaim(OpenIddictConstants.Claims.ClientId, Client).SetPresenters(Client)
            .SetAuthorizationId(authId).SetScopes(Scope)
            .SetTokenId(await tokens.GetIdAsync(user, token)).SetClaim(OpenIddictConstants.Claims.Private.DeviceCodeId, await tokens.GetIdAsync(device, token));
        var request = new OpenIddictRequest { ClientId = Client, Scope = Scope };
        await DevicePollAdmission.RegisterLockedAsync(scope, request, principal, token);
    }
}
