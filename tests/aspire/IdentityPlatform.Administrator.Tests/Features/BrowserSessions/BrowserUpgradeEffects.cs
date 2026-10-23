using System.Globalization;
using IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using IdentityPlatform.Infrastructure.Persistence;
using IdentityPlatform.Migrator.Features.AdministratorProtocol;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Asserts independent receipt, untouched native entries and actual new-revision native issuance.</summary>
internal static class BrowserUpgradeEffects
{
    private const string TargetRevision = "2";
    private const string ForeignClient = "unrelated-upgrade-fixture";
    private const string ExpectedActor = "deployment-operator";
    private const string ExpectedTarget = "administrator-protocol-profile";
    private const string ExpectedIdentifier = "1";
    private const string ExpectedOutcomePrefix = "upgraded-to:";

    /// <summary>Requires exactly one durable sanitized receipt with the independently expected transition and caller provenance.</summary>
    internal static async Task RequireReceiptAsync(AdministratorProtocolScenario scenario, AdministratorRegistrationInput input)
    {
        await using var scope = scenario.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var receipts = await database.Set<SecurityAuditIntent>().AsNoTracking().Where(row => row.Event == BrowserUpgradeFixture.AuditEvent)
            .ToArrayAsync(TestContext.Current.CancellationToken);
        var receipt = Assert.Single(receipts);
        Assert.Equal(input.OperationId, receipt.CorrelationId);
        Assert.Equal(input.OperatorReference, receipt.ActorId);
        Assert.Equal(ExpectedActor, receipt.ActorType);
        Assert.Equal(ExpectedTarget, receipt.TargetType);
        Assert.Equal(ExpectedIdentifier, receipt.TargetId);
        Assert.Equal(ExpectedOutcomePrefix + checked(input.ExpectedRevision!.Value + 1).ToString(CultureInfo.InvariantCulture), receipt.Outcome);
        Assert.Equal((await BrowserUpgradeFixture.ReadAsync(scenario)).Observed, receipt.OccurredAt);
    }

    /// <summary>Seeds manager-owned unrelated artifacts only as preservation sentinels; none substitutes for a genuine code/PAR invalidation proof.</summary>
    internal static Task<string[]> SeedUnrelatedAsync(AdministratorProtocolScenario scenario) => BrowserUpgradeFixture.OwnedAsync(scenario, async (scope, token) =>
    {
        await using var transaction = await ProtocolStorageTransaction.OpenAsync(scope.Owner);
        await AdministratorProtocolLocks.AcquireProfileAsync(scope.Owner, token);
        var applications = scope.Services.GetRequiredService<IOpenIddictApplicationManager>();
        var own = (await applications.FindByClientIdAsync(AdministratorProtocolIdentity.ClientId, token))!;
        var foreign = await applications.CreateAsync(new OpenIddictApplicationDescriptor { ClientId = ForeignClient, ClientType = OpenIddictConstants.ClientTypes.Public }, token);
        var manager = scope.Services.GetRequiredService<IOpenIddictTokenManager>();
        List<string> identifiers = [];
        foreach (var type in new[] { OpenIddictConstants.TokenTypeIdentifiers.AccessToken, OpenIddictConstants.TokenTypeIdentifiers.IdentityToken,
            OpenIddictConstants.TokenTypeIdentifiers.Private.AuthorizationCode, OpenIddictConstants.TokenTypeIdentifiers.Private.RequestToken })
        {
            var entry = await manager.CreateAsync(new OpenIddictTokenDescriptor
            {
                ApplicationId = await applications.GetIdAsync(type == OpenIddictConstants.TokenTypeIdentifiers.Private.RequestToken ? foreign : own, token),
                Type = type,
                Status = OpenIddictConstants.Statuses.Valid,
                CreationDate = scenario.Account.Journey.Clock.Now,
                ExpirationDate = scenario.Account.Journey.Clock.Now.AddMinutes(1)
            }, token);
            identifiers.Add((await manager.GetIdAsync(entry, token))!);
        }
        await transaction.CommitAsync(token);
        return identifiers.ToArray();
    });

    /// <summary>Fresh native-manager reads require every unrelated sentinel to retain its original valid status.</summary>
    internal static async Task RequireUnrelatedAsync(AdministratorProtocolScenario scenario, string[] identifiers)
    {
        foreach (var identifier in identifiers)
        {
            Assert.Equal(OpenIddictConstants.Statuses.Valid, await NativeAccountPersistence.StatusAsync(scenario, identifier));
        }
    }

    /// <summary>A separately B3-configured native root must issue and redeem a new revision-bound grant using the genuine account cookie.</summary>
    internal static async Task RequireFreshGrantAsync(AdministratorProtocolScenario scenario, IServiceProvider services)
    {
        var reference = await NativeAccountFlow.PushAsync(scenario, services);
        var code = NativeAccountFlow.ReadCode(scenario, await NativeAccountFlow.AuthorizeAsync(scenario, reference, services));
        var principal = await NativeAccountFlow.ValidateAsync(scenario, code, OpenIddictConstants.TokenTypeIdentifiers.Private.AuthorizationCode, services);
        Assert.Equal(TargetRevision, principal.GetClaim(IdentityPlatform.AuthServer.Features.Protocol.Administrator.AdministratorPrincipal.ProfileRevision));
        var before = await NativeAccountPersistence.CountAsync(scenario);
        await NativeMutationGrant.RequireAsync(scenario, await NativeAccountFlow.ExchangeAsync(scenario, code, services), principal, before, services);
    }
}
