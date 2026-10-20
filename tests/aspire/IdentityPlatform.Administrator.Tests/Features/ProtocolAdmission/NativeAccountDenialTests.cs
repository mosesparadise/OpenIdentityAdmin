using System.Text.Json;
using IdentityPlatform.Administrator.Tests.Features.Governance.Policy;
using IdentityPlatform.Administrator.Tests.Fixtures;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;

/// <summary>Proves real endpoint denials publish safe native errors only after durable consumption and later issuance rollback.</summary>
[Collection("administrator-bootstrap")]
public sealed class NativeAccountDenialTests(BootstrapFixture fixture)
{
    private const string Client = "identity-admin-bff";
    private const string LoginRequired = "login_required";
    private const string AccessDenied = "access_denied";
    private const string InvalidGrant = "invalid_grant";
    private const string ProviderTrait = "Provider";

    private const string Description = "The request could not be completed.";
    private const string State = "native-account-state";
    private const int RedirectFields = 4;
    private const int JsonFields = 2;
    private static readonly bool[] Endpoints = [true, false];
    private static readonly string[] Providers = ["PostgreSql", "SqlServer"];

    /// <summary>A real native Forbid before sign-in returns login_required without creating credentials or redeeming the still-valid PAR reference.</summary>
    [Theory, MemberData(nameof(ProviderCases))]
    public async Task Given_ValidParWithoutCookie_When_AuthorizationIsDenied_Then_NativeLoginRequiredPreservesUnredeemedRequest(string provider)
    {
        await using var scenario = await AdministratorProtocolScenario.CreateAsync(fixture, provider);
        var reference = await NativeAccountFlow.PushAsync(scenario);
        var identifier = await NativeAccountPersistence.ReferenceIdAsync(scenario, reference);
        var before = await NativeAccountPersistence.CountAsync(scenario);
        var response = await NativeAccountHttp.InvokeAsync(scenario, AdministratorProtocolScenario.AuthorizePath, new()
        {
            [OpenIddictConstants.Parameters.ClientId] = Client,
            [OpenIddictConstants.Parameters.RequestUri] = reference
        });
        RequireRedirect(scenario, response, LoginRequired);
        Assert.Equal(before, await NativeAccountPersistence.CountAsync(scenario));
        Assert.Equal(OpenIddictConstants.Statuses.Valid, await NativeAccountPersistence.StatusAsync(scenario, identifier));
    }

    /// <summary>Removal of the last current permission rejects actual issuance after native redemption and rolls back its later transaction.</summary>
    [Theory, MemberData(nameof(EndpointCases))]
    public async Task Given_LastPermissionRemoved_When_NativeIssuanceRechecksAuthority_Then_DenialRollsBackIssuanceAndKeepsConsumption(string provider, bool authorization)
    {
        await using var scenario = await AdministratorProtocolScenario.CreateAsync(fixture, provider);
        var reference = await NativeAccountFlow.PushAsync(scenario);
        var identifier = await NativeAccountPersistence.ReferenceIdAsync(scenario, reference);
        string? code = null;
        if (!authorization)
        {
            code = NativeAccountFlow.ReadCode(scenario, await NativeAccountFlow.AuthorizeAsync(scenario, reference));
            identifier = (await NativeAccountFlow.ValidateAsync(scenario, code, OpenIddictConstants.TokenTypeIdentifiers.Private.AuthorizationCode)).GetTokenId()!;
        }
        var before = await NativeAccountPersistence.CountAsync(scenario);
        await GrantState.SetMembershipsAsync(scenario.Account);
        var response = authorization ? await NativeAccountFlow.AuthorizeAsync(scenario, reference) : await NativeAccountFlow.ExchangeAsync(scenario, code!);
        if (authorization)
        {
            RequireRedirect(scenario, response, AccessDenied);
        }
        else
        {
            RequireJson(response, InvalidGrant);
        }
        Assert.Equal(before, await NativeAccountPersistence.CountAsync(scenario));
        Assert.True(await NativeAccountPersistence.IsRedeemedAsync(scenario, identifier));
    }

    /// <summary>Requires the fixed callback, original state/issuer and exactly the safe error fields, with no product cookie.</summary>
    internal static void RequireRedirect(AdministratorProtocolScenario scenario, NativeAccountResult response, string error)
    {
        RequireNoFailure(response);
        Assert.Equal(StatusCodes.Status302Found, response.Status);
        Assert.Empty(response.Body);
        var uri = new Uri(response.Location);
        Assert.True(uri.GetLeftPart(UriPartial.Path) == scenario.Material.Policy.RedirectUri);
        var query = QueryHelpers.ParseQuery(uri.Query);
        Assert.Equal(RedirectFields, query.Count);
        Assert.True(query[OpenIddictConstants.Parameters.Error].Count == 1 && query[OpenIddictConstants.Parameters.Error][0] == error);
        Assert.True(query[OpenIddictConstants.Parameters.ErrorDescription].Count == 1 && query[OpenIddictConstants.Parameters.ErrorDescription][0] == Description);
        Assert.True(query[OpenIddictConstants.Parameters.State].Count == 1 && query[OpenIddictConstants.Parameters.State][0] == State);
        Assert.True(query[OpenIddictConstants.Parameters.Iss].Count == 1 && query[OpenIddictConstants.Parameters.Iss][0] == scenario.Issuer.AbsoluteUri);
    }

    /// <summary>Requires exactly the safe native code-exchange error; success token fields or redirect headers cannot survive.</summary>
    internal static void RequireJson(NativeAccountResult response, string error)
    {
        RequireNoFailure(response);
        Assert.Equal(StatusCodes.Status400BadRequest, response.Status);
        Assert.Empty(response.Location);
        using var json = JsonDocument.Parse(response.Body);
        Assert.Equal(JsonFields, json.RootElement.EnumerateObject().Count());
        Assert.True(json.RootElement.GetProperty(OpenIddictConstants.Parameters.Error).GetString() == error);
        Assert.True(json.RootElement.GetProperty(OpenIddictConstants.Parameters.ErrorDescription).GetString() == Description);
    }

    /// <summary>Reports only exception type and code stack, never protocol payloads or personal data.</summary>
    private static void RequireNoFailure(NativeAccountResult response)
    {
        Assert.True(response.Failure is null, response.Failure?.GetType().FullName + Environment.NewLine + response.Failure?.StackTrace);
        Assert.False(response.HasCookie);
    }

    public static IEnumerable<TheoryDataRow<string>> ProviderCases => Providers.Select(provider => new TheoryDataRow<string>(provider).WithTrait(ProviderTrait, provider));
    public static IEnumerable<TheoryDataRow<string, bool>> EndpointCases => from provider in Providers
                                                                            from authorization in Endpoints
                                                                            select new TheoryDataRow<string, bool>(provider, authorization).WithTrait(ProviderTrait, provider);
}
