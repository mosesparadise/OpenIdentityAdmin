using IdentityPlatform.Infrastructure.Features.Identity.Journeys;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;

/// <summary>Demonstrates explicit invalidation of isolated disposable restored data; this is not a production restore or operator command.</summary>
internal static class NativeRestoreInvalidation
{
    /// <summary>Disables the fixed boundary, revokes the real account session and invalidates its native grants/tokens before any request resumes.</summary>
    internal static async Task ApplyAsync(AdministratorProtocolScenario scenario)
    {
        await NativeRegistrationDrift.ApplyAsync(scenario, NativeRegistrationDrift.Change.Disabled);
        await using (var scope = scenario.Account.CreateScope())
        {
            Assert.True(await scope.ServiceProvider.GetRequiredService<JourneyLogout>()
                .RevokeAsync(scenario.Account.Session, TestContext.Current.CancellationToken));
        }
        await new ProtocolStorageRequest(scenario.Account.Journey.Clock, TestContext.Current.CancellationToken)
            .RunAsync(scenario.Services.GetRequiredService<IServiceScopeFactory>(), async (scope, token) =>
            {
                scope.Services.GetRequiredService<ProtocolStorageSelection>().Select(ProtocolStorageMode.Owned);
                await using var transaction = await ProtocolStorageTransaction.OpenAsync(scope.Owner);
                await AdministratorProtocolLocks.AcquireProfileAsync(scope.Owner, token);
                var application = await scope.Owner.RunAsync(current => new ValueTask<string>(scope.Owner.Database
                    .Set<AdministratorProtocolProfile>().Select(row => row.ApplicationId).SingleAsync(current)), token);
                var before = await scope.Owner.RunAsync(async current => (
                    Tokens: await scope.Owner.Database.Set<OpenIddictEntityFrameworkCoreToken>().LongCountAsync(row => row.Application!.Id == application, current),
                    Authorizations: await scope.Owner.Database.Set<OpenIddictEntityFrameworkCoreAuthorization>().LongCountAsync(row => row.Application!.Id == application, current)), token);
                Assert.True(before.Tokens > 0 && before.Authorizations > 0);
                Assert.Equal(before.Tokens, await scope.Services.GetRequiredService<IOpenIddictTokenManager>().RevokeByApplicationIdAsync(application, token));
                Assert.Equal(before.Authorizations, await scope.Services.GetRequiredService<IOpenIddictAuthorizationManager>().RevokeByApplicationIdAsync(application, token));
                await transaction.CommitAsync(token);
                return true;
            });
    }

    /// <summary>Independently verifies persisted profile/session/grant/token invalidation rather than inferring all layers from one early callback rejection.</summary>
    internal static async Task RequireAsync(AdministratorProtocolScenario scenario)
    {
        await using var scope = scenario.Account.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var token = TestContext.Current.CancellationToken;
        var profile = await database.Set<AdministratorProtocolProfile>().AsNoTracking().SingleAsync(token);
        Assert.False(profile.Enabled);
        Assert.True(await database.Set<AdministratorSession>().AsNoTracking().Where(row => row.PublicId == scenario.Account.Session.SessionId)
            .Select(row => row.Revoked).SingleAsync(token));
        var statuses = await database.Set<OpenIddictEntityFrameworkCoreToken>().AsNoTracking().Where(row => row.Application!.Id == profile.ApplicationId)
            .Select(row => row.Status).ToArrayAsync(token);
        Assert.NotEmpty(statuses);
        Assert.All(statuses, status => Assert.Equal(OpenIddictConstants.Statuses.Revoked, status));
        var grants = await database.Set<OpenIddictEntityFrameworkCoreAuthorization>().AsNoTracking().Where(row => row.Application!.Id == profile.ApplicationId)
            .Select(row => row.Status).ToArrayAsync(token);
        Assert.NotEmpty(grants);
        Assert.All(grants, status => Assert.Equal(OpenIddictConstants.Statuses.Revoked, status));
    }
}
