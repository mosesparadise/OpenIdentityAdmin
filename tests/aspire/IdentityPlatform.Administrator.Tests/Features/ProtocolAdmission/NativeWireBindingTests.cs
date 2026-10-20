using IdentityPlatform.Administrator.Tests.Fixtures;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;

/// <summary>Checks original HTTP parameter bindings with genuine native grants and independent issuer roots.</summary>
[Collection("administrator-bootstrap")]
public sealed class NativeWireBindingTests(BootstrapFixture fixture)
{
    private const string ProviderTrait = "Provider";
    private const string AlternatePath = "/unregistered";
    private const string DifferentVerifier = "different-verifier-with-at-least-forty-three-characters";
    private const string ForeignClient = "unregistered-administrator-client";
    private static readonly string[] Providers = ["PostgreSql", "SqlServer"];
    public enum PushedChange
    {
        Redirect, Scope, ChallengeMethod, Client
    }
    public enum ExchangeChange
    {
        Redirect, Verifier
    }

    /// <summary>Each independently malformed PAR request must issue no native credential; a fresh valid demand on the same peer still succeeds.</summary>
    [Theory, MemberData(nameof(PushedCases))]
    public async Task Given_FixedRegistration_When_ParChangesRequiredBindings_Then_NativeRequestDeniesAndFreshFlowSucceeds(string provider, PushedChange change)
    {
        await using var scenario = await AdministratorProtocolScenario.CreateAsync(fixture, provider);
        await using var peer = scenario.CreatePeer(provider);
        await CompleteAsync(scenario, peer.Services);
        var fields = NativeAccountFlow.PushFields(scenario);
        switch (change)
        {
            case PushedChange.Redirect:
                fields[OpenIddictConstants.Parameters.RedirectUri] += AlternatePath;
                break;
            case PushedChange.Scope:
                fields[OpenIddictConstants.Parameters.Scope] = OpenIddictConstants.Scopes.OpenId;
                break;
            case PushedChange.ChallengeMethod:
                fields[OpenIddictConstants.Parameters.CodeChallengeMethod] = OpenIddictConstants.CodeChallengeMethods.Plain;
                break;
            case PushedChange.Client:
                fields[OpenIddictConstants.Parameters.ClientId] = ForeignClient;
                break;
        }
        var before = await NativeAccountPersistence.CountAsync(scenario);
        var response = await NativeAccountHttp.InvokeAsync(scenario, AdministratorProtocolScenario.ParPath, fields, services: peer.Services);
        NativeAccountDenialTests.RequireJson(response, change == PushedChange.Client
            ? OpenIddictConstants.Errors.InvalidClient : OpenIddictConstants.Errors.InvalidRequest);
        Assert.Equal(before, await NativeAccountPersistence.CountAsync(scenario));
        await CompleteAsync(scenario, peer.Services);
    }

    /// <summary>Malformed exchange bindings fail before native redemption; a fresh assertion with the original correct parameters can redeem once.</summary>
    [Theory, MemberData(nameof(ExchangeCases))]
    public async Task Given_RealCodeOnAnotherIssuer_When_ExchangeChangesRedirectOrVerifier_Then_NoCredentialIsIssuedAndCodeRemainsUsable(string provider, ExchangeChange change)
    {
        await using var scenario = await AdministratorProtocolScenario.CreateAsync(fixture, provider);
        await using var peer = scenario.CreatePeer(provider);
        var reference = await NativeAccountFlow.PushAsync(scenario);
        var code = NativeAccountFlow.ReadCode(scenario, await NativeAccountFlow.AuthorizeAsync(scenario, reference, peer.Services));
        var principal = await NativeAccountFlow.ValidateAsync(scenario, code, OpenIddictConstants.TokenTypeIdentifiers.Private.AuthorizationCode);
        var fields = NativeAccountFlow.ExchangeFields(scenario, code);
        if (change == ExchangeChange.Redirect)
        {
            fields[OpenIddictConstants.Parameters.RedirectUri] += AlternatePath;
        }
        else
        {
            fields[OpenIddictConstants.Parameters.CodeVerifier] = DifferentVerifier;
        }
        var before = await NativeAccountPersistence.CountAsync(scenario);
        var response = await NativeAccountHttp.InvokeAsync(scenario, AdministratorProtocolScenario.TokenPath, fields, services: peer.Services);
        NativeAccountDenialTests.RequireJson(response, OpenIddictConstants.Errors.InvalidGrant);
        Assert.Equal(before, await NativeAccountPersistence.CountAsync(scenario));
        Assert.Equal(OpenIddictConstants.Statuses.Valid, await NativeAccountPersistence.StatusAsync(scenario, principal.GetTokenId()!));
        await NativeMutationGrant.RequireAsync(scenario, await NativeAccountFlow.ExchangeAsync(scenario, code, peer.Services), principal, before);
    }

    /// <summary>Completes a fresh native flow with crypto and exact persisted effects, independent of any malformed request's assertion.</summary>
    private static async Task CompleteAsync(AdministratorProtocolScenario scenario, IServiceProvider peer)
    {
        var reference = await NativeAccountFlow.PushAsync(scenario, peer);
        var code = NativeAccountFlow.ReadCode(scenario, await NativeAccountFlow.AuthorizeAsync(scenario, reference));
        var principal = await NativeAccountFlow.ValidateAsync(scenario, code, OpenIddictConstants.TokenTypeIdentifiers.Private.AuthorizationCode);
        var before = await NativeAccountPersistence.CountAsync(scenario);
        await NativeMutationGrant.RequireAsync(scenario, await NativeAccountFlow.ExchangeAsync(scenario, code, peer), principal, before);
    }

    public static IEnumerable<TheoryDataRow<string, PushedChange>> PushedCases =>
        from provider in Providers
        from change in Enum.GetValues<PushedChange>()
        select new TheoryDataRow<string, PushedChange>(provider, change).WithTrait(ProviderTrait, provider);
    public static IEnumerable<TheoryDataRow<string, ExchangeChange>> ExchangeCases =>
        from provider in Providers
        from change in Enum.GetValues<ExchangeChange>()
        select new TheoryDataRow<string, ExchangeChange>(provider, change).WithTrait(ProviderTrait, provider);
}
