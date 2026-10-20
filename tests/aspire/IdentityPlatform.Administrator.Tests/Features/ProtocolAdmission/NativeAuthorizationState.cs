using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;

/// <summary>Creates explicit authorization-status or orphaned-binding test conditions while retaining the genuine protected code payload.</summary>
internal static class NativeAuthorizationState
{
    private const string ApplicationKey = "ApplicationId";
    private const string AuthorizationKey = "AuthorizationId";

    /// <summary>Uses native revocation or a foreign-key-respecting detach/delete inside one bounded serialized fixture transaction.</summary>
    internal static Task ChangeAsync(AdministratorProtocolScenario scenario, string code, string authorization, bool orphan) =>
        new ProtocolStorageRequest(scenario.Account.Journey.Clock, TestContext.Current.CancellationToken)
            .RunAsync(scenario.Services.GetRequiredService<IServiceScopeFactory>(), async (scope, token) =>
            {
                scope.Services.GetRequiredService<ProtocolStorageSelection>().Select(ProtocolStorageMode.Owned);
                await using var transaction = await ProtocolStorageTransaction.OpenAsync(scope.Owner);
                await AdministratorProtocolLocks.AcquireProfileAsync(scope.Owner, token);
                if (orphan)
                {
                    await scope.Owner.RunAsync(async current =>
                    {
                        Assert.Equal(1, await scope.Owner.Database.Set<OpenIddictEntityFrameworkCoreToken>().Where(row => row.Id == code)
                            .ExecuteUpdateAsync(update => update.SetProperty(row => EF.Property<string?>(row, AuthorizationKey), (string?)null), current));
                        Assert.Equal(1, await scope.Owner.Database.Set<OpenIddictEntityFrameworkCoreAuthorization>()
                            .Where(row => row.Id == authorization).ExecuteDeleteAsync(current));
                    }, token, write: true);
                }
                else
                {
                    var manager = scope.Services.GetRequiredService<IOpenIddictAuthorizationManager>();
                    var row = await manager.FindByIdAsync(authorization, token);
                    Assert.NotNull(row);
                    Assert.True(await manager.TryRevokeAsync(row, token));
                }
                await transaction.CommitAsync(token);
                return true;
            });

    /// <summary>Hashes immutable code fields without printing protected payloads or identifiers, deliberately excluding only the authorization FK.</summary>
    internal static async Task<string> CodeBindingAsync(AdministratorProtocolScenario scenario, string code)
    {
        await using var scope = scenario.Account.CreateScope();
        var value = await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().Set<OpenIddictEntityFrameworkCoreToken>()
            .AsNoTracking().Where(row => row.Id == code).Select(row => new
            {
                row.Id,
                row.Payload,
                row.ReferenceId,
                row.Status,
                row.Subject,
                row.Type,
                row.CreationDate,
                row.ExpirationDate,
                row.RedemptionDate,
                Application = EF.Property<string>(row, ApplicationKey)
            }).SingleAsync(TestContext.Current.CancellationToken);
        return FactorBinding.Digest(value);
    }

    /// <summary>Independently verifies committed authorization status or absence and the corresponding code-row relationship.</summary>
    internal static async Task RequireChangedAsync(AdministratorProtocolScenario scenario, string code, string authorization, bool orphan)
    {
        await using var scope = scenario.Account.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var token = TestContext.Current.CancellationToken;
        var status = await database.Set<OpenIddictEntityFrameworkCoreAuthorization>().AsNoTracking().Where(row => row.Id == authorization)
            .Select(row => row.Status).SingleOrDefaultAsync(token);
        var binding = await database.Set<OpenIddictEntityFrameworkCoreToken>().AsNoTracking().Where(row => row.Id == code)
            .Select(row => EF.Property<string?>(row, AuthorizationKey)).SingleAsync(token);
        Assert.True(orphan ? status is null && binding is null : status == OpenIddictConstants.Statuses.Revoked && binding == authorization);
    }
}
