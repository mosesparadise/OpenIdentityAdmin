using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Persistence.Tests.Features.ProtocolStorage;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;

/// <summary>Exercises actual native endpoint issuance at each retained server budget's last slot and rejects the next complete request.</summary>
[Collection("administrator-bootstrap")]
public sealed class NativeWireQuotaTests(BootstrapFixture fixture)
{
    private const string ProviderTrait = "Provider";
    private const int LiveRequestLimit = 128;
    private const int RetainedRequestLimit = 10_000;
    private const int AuthorizationLimit = 2_000;
    private const int TokenLimit = 20_000;
    private const int ExchangeRows = 2;
    private static readonly string[] Providers = ["PostgreSql", "SqlServer"];
    private static readonly bool[] RetentionModes = [false, true];

    /// <summary>Actual PAR creates the last live/retained request row; another signed demand cannot evict retained rows or exceed capacity.</summary>
    [Theory, MemberData(nameof(RequestCases))]
    public async Task Given_OneNativeParSlot_When_IndependentIssuerCreatesThenExceedsCapacity_Then_EqualitySucceedsAndNextRequestDenies(string provider, bool retained)
    {
        await using var scenario = await AdministratorProtocolScenario.CreateAsync(fixture, provider);
        await using var peer = scenario.CreatePeer(provider);
        var limit = retained ? RetainedRequestLimit : LiveRequestLimit;
        await NativeTokenBudgetFixture.FillAsync(scenario.Services, limit - 1, false,
            OpenIddictConstants.TokenTypeIdentifiers.Private.RequestToken,
            retained ? OpenIddictConstants.Statuses.Redeemed : OpenIddictConstants.Statuses.Valid, TestContext.Current.CancellationToken);
        var reference = await NativeAccountFlow.PushAsync(scenario, peer.Services);
        var identifier = await NativeAccountPersistence.ReferenceIdAsync(scenario, reference);
        Assert.Equal(((long)limit, 0L), await NativeAccountPersistence.CountAsync(scenario));
        var denied = await NativeAccountHttp.InvokeAsync(scenario, AdministratorProtocolScenario.ParPath, NativeAccountFlow.PushFields(scenario));
        NativeAccountDenialTests.RequireJson(denied, OpenIddictConstants.Errors.TemporarilyUnavailable);
        Assert.Equal(((long)limit, 0L), await NativeAccountPersistence.CountAsync(scenario));
        Assert.Equal(OpenIddictConstants.Statuses.Valid, await NativeAccountPersistence.StatusAsync(scenario, identifier));
        var code = NativeAccountFlow.ReadCode(scenario, await NativeAccountFlow.AuthorizeAsync(scenario, reference));
        await CompleteAsync(scenario, code, peer.Services);
    }

    /// <summary>Actual authorize fills the last authorization slot; the next PAR is consumed but no code/authorization is issued.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_OneAuthorizationSlot_When_TwoRealParRequestsAuthorize_Then_OnlyFirstCreatesCodeAndDeniedConsumptionRemains(string provider)
    {
        await using var scenario = await AdministratorProtocolScenario.CreateAsync(fixture, provider);
        await using var peer = scenario.CreatePeer(provider);
        var first = await NativeAccountFlow.PushAsync(scenario);
        var second = await NativeAccountFlow.PushAsync(scenario);
        var consumed = await NativeAccountPersistence.ReferenceIdAsync(scenario, second);
        await NativeTokenBudgetFixture.FillAsync(scenario.Services, AuthorizationLimit - 1, true,
            OpenIddictConstants.AuthorizationTypes.AdHoc, OpenIddictConstants.Statuses.Valid, TestContext.Current.CancellationToken);
        var code = NativeAccountFlow.ReadCode(scenario, await NativeAccountFlow.AuthorizeAsync(scenario, first, peer.Services));
        var before = await NativeAccountPersistence.CountAsync(scenario);
        Assert.Equal(AuthorizationLimit, before.Authorizations);
        NativeAccountDenialTests.RequireRedirect(scenario, await NativeAccountFlow.AuthorizeAsync(scenario, second), OpenIddictConstants.Errors.TemporarilyUnavailable);
        Assert.Equal(before, await NativeAccountPersistence.CountAsync(scenario));
        Assert.True(await NativeAccountPersistence.IsRedeemedAsync(scenario, consumed));
        await CompleteAsync(scenario, code, peer.Services);
    }

    /// <summary>Actual exchange fills the final two total-token slots, then all three native creation endpoints fail without additional rows.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_TwoRetainedTokenSlots_When_ExchangeFillsBudget_Then_ParAuthorizeAndExchangeCannotExceedIt(string provider)
    {
        await using var scenario = await AdministratorProtocolScenario.CreateAsync(fixture, provider);
        await using var peer = scenario.CreatePeer(provider);
        var first = await CodeAsync(scenario);
        var second = await CodeAsync(scenario);
        var reference = await NativeAccountFlow.PushAsync(scenario);
        var before = await NativeAccountPersistence.CountAsync(scenario);
        await NativeTokenBudgetFixture.FillAsync(scenario.Services, checked(TokenLimit - ExchangeRows - (int)before.Tokens), false,
            OpenIddictConstants.TokenTypeIdentifiers.AccessToken, OpenIddictConstants.Statuses.Valid, TestContext.Current.CancellationToken);
        await CompleteAsync(scenario, first, peer.Services);
        var full = await NativeAccountPersistence.CountAsync(scenario);
        Assert.Equal(TokenLimit, full.Tokens);
        NativeAccountDenialTests.RequireJson(await NativeAccountHttp.InvokeAsync(scenario, AdministratorProtocolScenario.ParPath,
            NativeAccountFlow.PushFields(scenario), services: peer.Services), OpenIddictConstants.Errors.TemporarilyUnavailable);
        Assert.Equal(full, await NativeAccountPersistence.CountAsync(scenario));
        var requestId = await NativeAccountPersistence.ReferenceIdAsync(scenario, reference);
        NativeAccountDenialTests.RequireRedirect(scenario, await NativeAccountFlow.AuthorizeAsync(scenario, reference, peer.Services), OpenIddictConstants.Errors.TemporarilyUnavailable);
        Assert.True(await NativeAccountPersistence.IsRedeemedAsync(scenario, requestId));
        Assert.Equal(full, await NativeAccountPersistence.CountAsync(scenario));
        var principal = await NativeAccountFlow.ValidateAsync(scenario, second, OpenIddictConstants.TokenTypeIdentifiers.Private.AuthorizationCode);
        NativeAccountDenialTests.RequireJson(await NativeAccountFlow.ExchangeAsync(scenario, second, peer.Services), OpenIddictConstants.Errors.TemporarilyUnavailable);
        Assert.True(await NativeAccountPersistence.IsRedeemedAsync(scenario, principal.GetTokenId()!));
        Assert.Equal(full, await NativeAccountPersistence.CountAsync(scenario));
    }

    /// <summary>Prepares genuine authorization code material before capacity is filled, preserving native registration and current authority.</summary>
    private static async Task<string> CodeAsync(AdministratorProtocolScenario scenario) =>
        NativeAccountFlow.ReadCode(scenario, await NativeAccountFlow.AuthorizeAsync(scenario, await NativeAccountFlow.PushAsync(scenario)));

    /// <summary>Requires independently validated native code/access/ID artifacts and exact persisted exchange deltas for the successful last slot.</summary>
    private static async Task CompleteAsync(AdministratorProtocolScenario scenario, string code, IServiceProvider peer)
    {
        var principal = await NativeAccountFlow.ValidateAsync(scenario, code, OpenIddictConstants.TokenTypeIdentifiers.Private.AuthorizationCode);
        var before = await NativeAccountPersistence.CountAsync(scenario);
        await NativeMutationGrant.RequireAsync(scenario, await NativeAccountFlow.ExchangeAsync(scenario, code, peer), principal, before);
    }

    public static IEnumerable<TheoryDataRow<string, bool>> RequestCases => from provider in Providers
                                                                           from retained in RetentionModes
                                                                           select new TheoryDataRow<string, bool>(provider, retained).WithTrait(ProviderTrait, provider);
}
