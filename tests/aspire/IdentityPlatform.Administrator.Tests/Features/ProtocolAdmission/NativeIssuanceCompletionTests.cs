using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using Microsoft.AspNetCore.Http;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;

/// <summary>Proves native code/exchange publication and persisted outcomes at actual issuance commit/cancellation boundaries.</summary>
[Collection("administrator-bootstrap")]
public sealed class NativeIssuanceCompletionTests(BootstrapFixture fixture)
{
    private const string ProviderTrait = "Provider";
    private const int ExchangeArtifacts = 2;
    private static readonly string[] Providers = ["PostgreSql", "SqlServer"];
    private static readonly bool[] Endpoints = [true, false];

    /// <summary>Genuine exchange controls bracket a reached issuance fault while independent reads distinguish rollback from retained committed credentials.</summary>
    [Theory, MemberData(nameof(Cases))]
    public async Task Given_DurableConsumptionAndNativeGeneration_When_IssuanceCompletionFails_Then_NoOutputEscapesAndCommittedOutcomeIsReconciled(
        string provider, bool authorization, NativeIssuanceFailureMode mode)
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var fault = new NativeIssuanceCompletionFault(authorization, mode, cancellation);
        await using var scenario = await AdministratorProtocolScenario.CreateAsync(fixture, provider, configure: fault.Configure);
        await PositiveAsync(scenario);
        var reference = await NativeAccountFlow.PushAsync(scenario);
        var consumed = await NativeAccountPersistence.ReferenceIdAsync(scenario, reference);
        var fields = new Dictionary<string, string>
        {
            [OpenIddictConstants.Parameters.ClientId] = AdministratorProtocolIdentity.ClientId,
            [OpenIddictConstants.Parameters.RequestUri] = reference
        };
        if (!authorization)
        {
            var code = NativeAccountFlow.ReadCode(scenario, await NativeAccountFlow.AuthorizeAsync(scenario, reference));
            consumed = (await NativeAccountFlow.ValidateAsync(scenario, code, OpenIddictConstants.TokenTypeIdentifiers.Private.AuthorizationCode)).GetTokenId()!;
            fields = NativeAccountFlow.ExchangeFields(scenario, code);
        }
        var before = await NativeAccountPersistence.CountAsync(scenario);
        fault.Armed = true;
        try
        {
            var result = await NativeAccountHttp.InvokeAsync(scenario, authorization ? AdministratorProtocolScenario.AuthorizePath : AdministratorProtocolScenario.TokenPath,
                fields, cookie: authorization, cancellation: cancellation.Token);
            RequireFailure(result, fault, mode);
        }
        finally { fault.Armed = false; }
        Assert.True(await NativeAccountPersistence.IsRedeemedAsync(scenario, consumed));
        await RequireOutcomeAsync(scenario, fault, authorization, mode, before);
        await PositiveAsync(scenario);
        Assert.Equal(1, fault.Attempts);
        Assert.Equal(1, fault.Faults);
    }

    /// <summary>Requires actual request abort for explicit cancellation; other failures produce exactly unavailable with no staged native material.</summary>
    private static void RequireFailure(NativeAccountResult result, NativeIssuanceCompletionFault fault, NativeIssuanceFailureMode mode)
    {
        Assert.True(fault.Contains(result.Failure), result.Failure?.GetType().FullName);
        Assert.Equal(mode == NativeIssuanceFailureMode.Cancellation, result.Aborted);
        if (mode != NativeIssuanceFailureMode.Cancellation)
        {
            Assert.Equal(StatusCodes.Status503ServiceUnavailable, result.Status);
        }
        Assert.False(result.Claimed);
        Assert.True(result.Body.Length == 0);
        Assert.True(result.Location.Length == 0);
        Assert.False(result.HasCookie);
        Assert.Equal(1, fault.Attempts);
        Assert.Equal(1, fault.Faults);
        Assert.Equal(mode == NativeIssuanceFailureMode.CommitObservation ? 1 : 0, fault.Commits);
    }

    /// <summary>Checks exact retained-row deltas and independently validates committed artifacts without using suppressed output as a client response.</summary>
    private static async Task RequireOutcomeAsync(AdministratorProtocolScenario scenario, NativeIssuanceCompletionFault fault,
        bool authorization, NativeIssuanceFailureMode mode, (long Tokens, long Authorizations) before)
    {
        var committed = mode == NativeIssuanceFailureMode.CommitObservation;
        var expected = authorization ? 1 : ExchangeArtifacts;
        Assert.Equal(expected, fault.Artifacts.Count);
        Assert.True(fault.Artifacts.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(authorization ?
            [OpenIddictConstants.TokenTypeIdentifiers.Private.AuthorizationCode] :
            [OpenIddictConstants.TokenTypeIdentifiers.AccessToken, OpenIddictConstants.TokenTypeIdentifiers.IdentityToken]));
        Assert.Equal((before.Tokens + (committed ? expected : 0), before.Authorizations + (committed && authorization ? 1 : 0)),
            await NativeAccountPersistence.CountAsync(scenario));
        foreach (var (type, artifact) in fault.Artifacts)
        {
            Assert.Equal(committed ? OpenIddictConstants.Statuses.Valid : null, await NativeAccountPersistence.StatusAsync(scenario, artifact.Identifier));
            if (committed)
            {
                var principal = type == OpenIddictConstants.TokenTypeIdentifiers.IdentityToken ?
                    await NativeIdentityTokenOracle.ValidateAsync(scenario, artifact.Value, fault.Artifacts[OpenIddictConstants.TokenTypeIdentifiers.AccessToken].Value) :
                    await NativeAccountFlow.ValidateAsync(scenario, artifact.Value, type);
                Assert.True(principal.GetTokenId() == artifact.Identifier);
                Assert.True(principal.GetAuthorizationId() == artifact.Authorization);
                Assert.True(principal.GetClaim(OpenIddictConstants.Claims.Subject) == scenario.Account.ActorId);
            }
        }
    }

    /// <summary>Requires a full real code exchange and independent access/ID cryptographic checks before and after each fault.</summary>
    private static async Task PositiveAsync(AdministratorProtocolScenario scenario)
    {
        var reference = await NativeAccountFlow.PushAsync(scenario);
        var code = NativeAccountFlow.ReadCode(scenario, await NativeAccountFlow.AuthorizeAsync(scenario, reference));
        var principal = await NativeAccountFlow.ValidateAsync(scenario, code, OpenIddictConstants.TokenTypeIdentifiers.Private.AuthorizationCode);
        var before = await NativeAccountPersistence.CountAsync(scenario);
        await NativeMutationGrant.RequireAsync(scenario, await NativeAccountFlow.ExchangeAsync(scenario, code), principal, before);
    }

    public static IEnumerable<TheoryDataRow<string, bool, NativeIssuanceFailureMode>> Cases => from provider in Providers
                                                                                               from authorization in Endpoints
                                                                                               from mode in Enum.GetValues<NativeIssuanceFailureMode>()
                                                                                               select new TheoryDataRow<string, bool, NativeIssuanceFailureMode>(provider, authorization, mode).WithTrait(ProviderTrait, provider);
}
