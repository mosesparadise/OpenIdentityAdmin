using System.Text.Json;
using IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using Microsoft.AspNetCore.Http;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Proves protected native representation across real provider-backed roots without pretending test data is fresh-login completion authority.</summary>
[Collection("administrator-bootstrap")]
public sealed class NativeBrowserBindingTests(BootstrapFixture fixture)
{
    private const string Intent = "AAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8";
    private const string Completion = "ICEiIyQlJicoKSorLC0uLzAxMjM0NTY3ODk6Ozw9Pj8";
    private const string Other = "QEFCQ0RFRkdISUpLTE1OT1BRUlNUVVZXWFlaW1xdXl8";
    private const string Digest = "630DCD2966C4336691125448BBB25B4FF412A49C732DB2C8ABC1B8581BD710DD";
    private const string IntentField = "admin_login_intent";
    private const string CompletionField = "admin_login_completion";
    private const string SourceExpiry = "admin_session_expires_at";
    private const string ForeignClient = "foreign-browser-binding-client";
    private const string ProviderTrait = "Provider";
    private static readonly string[] ProviderNames = ["PostgreSql", "SqlServer"];
    private static readonly bool[] Booleans = [false, true];

    /// <summary>Native restoration ignores outer substitutions; private test transfer survives code protection and exchange but releases no B3 public claims.</summary>
    [Theory, MemberData(nameof(Variants))]
    public async Task Given_PrivateBinding_When_NativeParAndCodeCrossIndependentRoots_Then_OuterParametersCannotReplaceProtectedData(string provider, bool substitute)
    {
        var probe = new NativeBrowserBindingProbe(transfer: true);
        await using var scenario = await AdministratorProtocolScenario.CreateAsync(fixture, provider, configure: probe.Configure);
        await using var peer = scenario.CreatePeer(provider, probe.Configure);
        var reference = await PushAsync(scenario);
        var fields = new Dictionary<string, string>
        {
            [OpenIddictConstants.Parameters.ClientId] = AdministratorProtocolIdentity.ClientId,
            [OpenIddictConstants.Parameters.RequestUri] = reference
        };
        if (substitute)
        {
            fields.Add(IntentField, Other);
            fields.Add(CompletionField, Other);
        }
        var code = NativeAccountFlow.ReadCode(scenario, await NativeAccountHttp.InvokeAsync(scenario,
            AdministratorProtocolScenario.AuthorizePath, fields, cookie: true, services: peer.Services));
        Assert.True(probe.Copied);
        Assert.NotNull(probe.Restored);
        Assert.True(probe.Restored.Intent == Intent && probe.Restored.Completion == Completion && probe.Restored.IntentDigest == Digest);
        var principal = await NativeAccountFlow.ValidateAsync(scenario, code, OpenIddictConstants.TokenTypeIdentifiers.Private.AuthorizationCode);
        Assert.True(probe.Restored.Matches(principal));
        var exchange = NativeAccountFlow.ExchangeFields(scenario, code);
        if (substitute)
        {
            exchange.Add(IntentField, Other);
            exchange.Add(CompletionField, Other);
        }
        await RequireOrdinaryTokensAsync(scenario, await NativeAccountHttp.InvokeAsync(scenario, AdministratorProtocolScenario.TokenPath, exchange));
        Assert.True(probe.Exchanged);
    }

    /// <summary>The production B2 path does not turn carried selectors into code binding or authenticated fresh-ceremony claims.</summary>
    [Theory, MemberData(nameof(Providers))]
    public async Task Given_OnlyTransportedPair_When_NoTestTransferIsInstalled_Then_ExistingB2FlowGrantsNoB3Binding(string provider)
    {
        var probe = new NativeBrowserBindingProbe(transfer: false);
        await using var scenario = await AdministratorProtocolScenario.CreateAsync(fixture, provider, configure: probe.Configure);
        var reference = await PushAsync(scenario);
        var code = NativeAccountFlow.ReadCode(scenario, await NativeAccountFlow.AuthorizeAsync(scenario, reference));
        Assert.NotNull(probe.Restored);
        Assert.False(probe.Copied);
        var principal = await NativeAccountFlow.ValidateAsync(scenario, code, OpenIddictConstants.TokenTypeIdentifiers.Private.AuthorizationCode);
        Assert.Equal(BrowserLoginBindingState.Absent, BrowserLoginBinding.Read(principal, out _));
        await RequireOrdinaryTokensAsync(scenario, await NativeAccountFlow.ExchangeAsync(scenario, code));
        Assert.True(probe.Exchanged);
    }

    /// <summary>Extra transport fields cannot override fixed-client authentication or native flow permission checks.</summary>
    [Theory, MemberData(nameof(Variants))]
    public async Task Given_TransportedPair_When_ClientOrResponseFlowIsForeign_Then_NativeAdmissionStillDenies(string provider, bool foreignClient)
    {
        await using var scenario = await AdministratorProtocolScenario.CreateAsync(fixture, provider);
        _ = await PushAsync(scenario);
        var before = await NativeAccountPersistence.CountAsync(scenario);
        var fields = Fields(scenario);
        fields[foreignClient ? OpenIddictConstants.Parameters.ClientId : OpenIddictConstants.Parameters.ResponseType] =
            foreignClient ? ForeignClient : OpenIddictConstants.ResponseTypes.Token;
        var response = await NativeAccountHttp.InvokeAsync(scenario, AdministratorProtocolScenario.ParPath, fields);
        Assert.False(response.Aborted);
        NativeAccountDenialTests.RequireJson(response, foreignClient
            ? OpenIddictConstants.Errors.InvalidClient : OpenIddictConstants.Errors.UnsupportedResponseType);
        Assert.Equal(before, await NativeAccountPersistence.CountAsync(scenario));
        using var json = JsonDocument.Parse(response.Body);
        Assert.False(json.RootElement.TryGetProperty(OpenIddictConstants.Parameters.AccessToken, out _));
        Assert.False(json.RootElement.TryGetProperty(OpenIddictConstants.Parameters.IdToken, out _));
        Assert.False(json.RootElement.TryGetProperty(OpenIddictConstants.Parameters.RequestUri, out _));
        _ = await PushAsync(scenario);
    }

    /// <summary>Obtains an actual persisted PAR using a fresh signed client assertion and synthetic non-authoritative selectors.</summary>
    private static async Task<string> PushAsync(AdministratorProtocolScenario scenario) => NativeAccountFlow.ReadReference(
        await NativeAccountHttp.InvokeAsync(scenario, AdministratorProtocolScenario.ParPath, Fields(scenario)));

    /// <summary>Leaves all existing B2 proof and destination fields intact.</summary>
    private static Dictionary<string, string> Fields(AdministratorProtocolScenario scenario)
    {
        var fields = NativeAccountFlow.PushFields(scenario);
        fields.Add(IntentField, Intent);
        fields.Add(CompletionField, Completion);
        return fields;
    }

    /// <summary>Validates actual returned artifacts independently and checks that neither private selectors nor B3 public claims leak.</summary>
    private static async Task RequireOrdinaryTokensAsync(AdministratorProtocolScenario scenario, NativeAccountResult response)
    {
        NativeAccountFlow.RequireSuccess(response, StatusCodes.Status200OK);
        using var json = JsonDocument.Parse(response.Body);
        var accessValue = json.RootElement.GetProperty(OpenIddictConstants.Parameters.AccessToken).GetString()!;
        var access = await NativeAccountFlow.ValidateAsync(scenario, accessValue, OpenIddictConstants.TokenTypeIdentifiers.AccessToken);
        var identity = await NativeIdentityTokenOracle.ValidateAsync(scenario,
            json.RootElement.GetProperty(OpenIddictConstants.Parameters.IdToken).GetString()!, accessValue);
        foreach (var principal in new[] { access, identity })
        {
            Assert.Equal(BrowserLoginBindingState.Absent, BrowserLoginBinding.Read(principal, out _));
            Assert.DoesNotContain(principal.Claims, claim => claim.Type is IntentField or CompletionField or SourceExpiry);
            Assert.DoesNotContain(principal.Claims, claim => claim.Value is Intent or Completion or Digest);
        }
        Assert.False(response.HasCookie);
    }

    /// <summary>Preserves explicit provider traits for strict native inventory and report reconciliation.</summary>
    public static IEnumerable<TheoryDataRow<string>> Providers => ProviderNames.Select(provider => new TheoryDataRow<string>(provider).WithTrait(ProviderTrait, provider));

    /// <summary>Discovers both provider and substitution/foreign-flow dimensions independently.</summary>
    public static IEnumerable<TheoryDataRow<string, bool>> Variants => from provider in ProviderNames
                                                                       from value in Booleans
                                                                       select new TheoryDataRow<string, bool>(provider, value).WithTrait(ProviderTrait, provider);
}
