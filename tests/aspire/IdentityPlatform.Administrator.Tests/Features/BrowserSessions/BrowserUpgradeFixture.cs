using System.Text.Json;
using IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using IdentityPlatform.Migrator.Features.AdministratorProtocol;
using IdentityPlatform.Migrator.Features.AdministratorProtocol.Upgrade;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Reads independent committed upgrade effects and prepares isolated explicit external metadata defects.</summary>
internal static class BrowserUpgradeFixture
{
    internal const string Rejection = "Administrator browser registration upgrade was rejected.";
    internal const string AuditEvent = "administrator-browser-upgrade";
    internal const string Operator = "browser-upgrade-fixture";
    internal const string Operation = "upgrade-administrator-browser-registration";
    private const string ChangedOperator = "different-upgrade-operator";
    private const string ChangedOutcome = "upgraded-to:999";
    private const string ExtraProperty = "unexpected-upgrade-metadata";
    private const string MixedStatus = "VALID";

    /// <summary>Captures one original revision and operation identifier for explicit command reconciliation.</summary>
    internal static AdministratorRegistrationInput Input(AdministratorProtocolScenario scenario) =>
        new(Guid.NewGuid(), Operator, scenario.Material.Policy, 1);

    /// <summary>Invokes the actual production upgrade owner, never a fixture substitute for its transaction.</summary>
    internal static Task<bool> UpgradeAsync(AdministratorProtocolScenario scenario, AdministratorRegistrationInput input, IServiceProvider? services = null,
        CancellationToken? token = null) => AdministratorRegistrationUpgrade.RunAsync(services ?? scenario.Services, input, token ?? TestContext.Current.CancellationToken);

    /// <summary>Requires the finite product precondition denial rather than crediting an unrelated fixture/database failure.</summary>
    internal static async Task RejectAsync(AdministratorProtocolScenario scenario, AdministratorRegistrationInput input)
    {
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => UpgradeAsync(scenario, input));
        Assert.Equal(Rejection, error.Message);
    }

    /// <summary>Reads profile, native metadata, tokens and audit through a fresh scope after all command cleanup completes.</summary>
    internal static Task<Snapshot> ReadAsync(AdministratorProtocolScenario scenario) =>
        OwnedAsync(scenario, (scope, token) => new ValueTask<Snapshot>(ReadCoreAsync(scope.Services, scenario.Material.Policy, token)));

    /// <summary>Allows command-only fixtures to observe the same native metadata without enrolling an administrator.</summary>
    internal static async Task<Snapshot> ReadAsync(IServiceProvider services, AdministratorRegistrationPolicy policy)
    {
        await using var scope = services.CreateAsyncScope();
        return await ReadCoreAsync(scope.ServiceProvider, policy, TestContext.Current.CancellationToken);
    }

    /// <summary>Populates exact native descriptors from the already activated independent reader scope.</summary>
    private static async Task<Snapshot> ReadCoreAsync(IServiceProvider services, AdministratorRegistrationPolicy policy, CancellationToken token)
    {
        var database = services.GetRequiredService<IdentityPlatform.Infrastructure.Persistence.PlatformDbContext>();
        var profile = await database.Set<AdministratorProtocolProfile>().AsNoTracking().SingleAsync(token);
        var applications = services.GetRequiredService<IOpenIddictApplicationManager>();
        var scopes = services.GetRequiredService<IOpenIddictScopeManager>();
        var app = await database.Set<OpenIddictEntityFrameworkCoreApplication>().AsNoTracking().SingleAsync(row => row.Id == profile.ApplicationId, token);
        var resource = await database.Set<OpenIddictEntityFrameworkCoreScope>().AsNoTracking().SingleAsync(row => row.Id == profile.ScopeId, token);
        Assert.NotNull(app);
        Assert.NotNull(resource);
        var descriptor = new OpenIddictApplicationDescriptor();
        var resourceDescriptor = new OpenIddictScopeDescriptor();
        await applications.PopulateAsync(descriptor, app, token);
        await scopes.PopulateAsync(resourceDescriptor, resource, token);
        var audits = await database.Set<SecurityAuditIntent>().AsNoTracking().Where(row => row.Event == AuditEvent).OrderBy(row => row.Id).ToArrayAsync(token);
        var tokens = await database.Set<OpenIddictEntityFrameworkCoreToken>().AsNoTracking().OrderBy(row => row.Id)
            .Select(row => new { row.Id, row.Status, row.Type }).ToArrayAsync(token);
        return new(profile.ApplicationId, profile.ScopeId, profile.Revision, profile.LockRevision, profile.ObservedAt, profile.Enabled,
            policy.Matches(descriptor, resourceDescriptor), policy.ForBrowserSessions()!.Matches(descriptor, resourceDescriptor),
            JsonSerializer.Serialize(descriptor), JsonSerializer.Serialize(resourceDescriptor), JsonSerializer.Serialize(tokens), JsonSerializer.Serialize(audits));
    }

    /// <summary>Changes only the named test-owned precondition and commits it before observing upgrade behavior.</summary>
    internal static Task<bool> ChangeAsync(AdministratorProtocolScenario scenario, UpgradeDrift drift, AdministratorRegistrationInput input) =>
        OwnedAsync(scenario, async (scope, token) =>
        {
            await using var transaction = await ProtocolStorageTransaction.OpenAsync(scope.Owner);
            await AdministratorProtocolLocks.AcquireProfileAsync(scope.Owner, token);
            await ChangeProfileAsync(scope, drift, input, token);
            await ChangeNativeAsync(scope, drift, token);
            await transaction.CommitAsync(token);
            return true;
        });

    /// <summary>Seeds profile and receipt drift through the same owned connection while keeping the intervention separate from the real command.</summary>
    private static ValueTask ChangeProfileAsync(ProtocolStorageScope scope, UpgradeDrift drift, AdministratorRegistrationInput input, CancellationToken token) =>
        scope.Owner.RunAsync(async current =>
        {
            var profile = await scope.Owner.Database.Set<AdministratorProtocolProfile>().SingleAsync(current);
            if (drift == UpgradeDrift.DisabledProfile)
            {
                profile.Enabled = false;
                profile.Revision++;
            }
            if (drift == UpgradeDrift.Revision)
            {
                profile.Revision++;
            }
            if (drift is UpgradeDrift.ReceiptActor or UpgradeDrift.ReceiptOutcome or UpgradeDrift.DuplicateReceipt)
            {
                var receipt = await scope.Owner.Database.Set<SecurityAuditIntent>().SingleAsync(row => row.CorrelationId == input.OperationId, current);
                if (drift == UpgradeDrift.ReceiptActor)
                {
                    receipt.ActorId = ChangedOperator;
                }
                if (drift == UpgradeDrift.ReceiptOutcome)
                {
                    receipt.Outcome = ChangedOutcome;
                }
                if (drift == UpgradeDrift.DuplicateReceipt)
                {
                    scope.Owner.Database.Add(new SecurityAuditIntent
                    {
                        Event = receipt.Event,
                        ActorType = receipt.ActorType,
                        ActorId = receipt.ActorId,
                        TargetType = receipt.TargetType,
                        TargetId = receipt.TargetId,
                        Outcome = receipt.Outcome,
                        CorrelationId = receipt.CorrelationId,
                        OccurredAt = receipt.OccurredAt
                    });
                }
            }
            await scope.Owner.Database.SaveChangesAsync(current);
        }, token, write: true);

    /// <summary>Seeds exact native marker or request metadata drift that must never be silently normalized during an upgrade.</summary>
    private static async ValueTask ChangeNativeAsync(ProtocolStorageScope scope, UpgradeDrift drift, CancellationToken token)
    {
        var applications = scope.Services.GetRequiredService<IOpenIddictApplicationManager>();
        var app = (await applications.FindByClientIdAsync(AdministratorProtocolIdentity.ClientId, token))!;
        var descriptor = new OpenIddictApplicationDescriptor();
        await applications.PopulateAsync(descriptor, app, token);
        if (drift == UpgradeDrift.ApplicationDisabled)
        {
            descriptor.Properties[AdministratorRegistrationPolicy.EnabledProperty] = JsonSerializer.SerializeToElement(false);
        }
        if (drift == UpgradeDrift.ExtraProperty)
        {
            descriptor.Properties[ExtraProperty] = JsonSerializer.SerializeToElement(true);
        }
        if (drift is UpgradeDrift.ApplicationDisabled or UpgradeDrift.ExtraProperty)
        {
            await applications.UpdateAsync(app, descriptor, token);
        }
        if (drift == UpgradeDrift.ScopeDisabled)
        {
            var scopes = scope.Services.GetRequiredService<IOpenIddictScopeManager>();
            var resource = (await scopes.FindByNameAsync(AdministratorProtocolIdentity.Scope, token))!;
            var value = new OpenIddictScopeDescriptor();
            await scopes.PopulateAsync(value, resource, token);
            value.Properties[AdministratorRegistrationPolicy.EnabledProperty] = JsonSerializer.SerializeToElement(false);
            await scopes.UpdateAsync(resource, value, token);
        }
        if (drift is UpgradeDrift.RequestCase or UpgradeDrift.StatusCase)
        {
            await scope.Owner.RunAsync(async current =>
            {
                var row = await scope.Owner.Database.Set<OpenIddictEntityFrameworkCoreToken>().SingleAsync(item =>
                    item.Type == OpenIddictConstants.TokenTypeIdentifiers.Private.RequestToken && item.Status == OpenIddictConstants.Statuses.Valid, current);
                if (drift == UpgradeDrift.RequestCase)
                {
                    row.Type = OpenIddictConstants.TokenTypeIdentifiers.Private.RequestToken.ToUpperInvariant();
                }
                else
                {
                    row.Status = MixedStatus;
                }
                await scope.Owner.Database.SaveChangesAsync(current);
            }, token, write: true);
        }
    }

    /// <summary>Supplies a fully owned native test intervention under unchanged request budgets.</summary>
    internal static Task<T> OwnedAsync<T>(AdministratorProtocolScenario scenario, Func<ProtocolStorageScope, CancellationToken, ValueTask<T>> action) =>
        new ProtocolStorageRequest(scenario.Account.Journey.Clock, TestContext.Current.CancellationToken).RunAsync(
            scenario.Services.GetRequiredService<IServiceScopeFactory>(), (scope, token) =>
            {
                scope.Services.GetRequiredService<ProtocolStorageSelection>().Select(ProtocolStorageMode.Owned);
                return action(scope, token);
            });

    /// <summary>Retains safe metadata for exact before/after comparison; snapshots are never serialized to product diagnostics.</summary>
    internal sealed record Snapshot(string Application, string Scope, long Revision, long LockRevision, long Observed, bool Enabled,
        bool Legacy, bool Browser, string Descriptor, string ScopeDescriptor, string Tokens, string Audits);
}

/// <summary>Names supported disabled state separately from rejected external native/profile/receipt drift.</summary>
public enum UpgradeDrift
{
    DisabledProfile, Revision, ApplicationDisabled, ScopeDisabled, ExtraProperty, RequestCase, StatusCase, ReceiptActor, ReceiptOutcome, DuplicateReceipt
}
