using IdentityPlatform.AuthServer.Features.Protocol.Administrator;
using Microsoft.Extensions.Primitives;
using OpenIddict.Server;

namespace IdentityPlatform.Host.Tests.Features.AdministratorProtocol;

/// <summary>Checks raw multiplicity and fixed-profile authentication boundaries before native assertion parsing.</summary>
public sealed class AdministratorTransportTests
{
    private const string ClientId = "client_id";
    private const string Client = "identity-admin-bff";
    private const string Assertion = "client_assertion";
    private const string AssertionType = "client_assertion_type";
    private const string JwtBearer = "urn:ietf:params:oauth:client-assertion-type:jwt-bearer";
    private const string RequestUri = "request_uri";
    private const string Reference = "urn:synthetic-reference";
    private const string PilotClient = "identityplatform-pilot";
    private const string Code = "code";
    private const string AliasedClient = "CLIENT_ID";
    private const string AliasedAssertion = "CLIENT_ASSERTION";
    private const string Secret = "client_secret";
    private const string Resource = "resource";
    private const string Audience = "audience";
    private const string SyntheticProof = "synthetic-proof";

    /// <summary>Each control changes one raw ambiguity, override or authentication-method requirement.</summary>
    [Theory]
    [InlineData(AdministratorTransportCase.Valid, true)]
    [InlineData(AdministratorTransportCase.MissingClient, false)]
    [InlineData(AdministratorTransportCase.WrongClient, false)]
    [InlineData(AdministratorTransportCase.DuplicateClient, false)]
    [InlineData(AdministratorTransportCase.DuplicateOtherField, false)]
    [InlineData(AdministratorTransportCase.AliasedClient, false)]
    [InlineData(AdministratorTransportCase.AliasedAssertion, false)]
    [InlineData(AdministratorTransportCase.EmptySecret, false)]
    [InlineData(AdministratorTransportCase.EmptyResource, false)]
    [InlineData(AdministratorTransportCase.EmptyAudience, false)]
    [InlineData(AdministratorTransportCase.MissingAssertion, false)]
    [InlineData(AdministratorTransportCase.EmptyAssertion, false)]
    [InlineData(AdministratorTransportCase.WrongAssertionType, false)]
    public void Given_RawTokenFields_When_ValidatedBeforeExtraction_Then_AmbiguousOrMixedMethodInputIsDenied(AdministratorTransportCase scenario, bool expected)
    {
        var fields = new Dictionary<string, StringValues> { [ClientId] = Client, [Assertion] = SyntheticProof, [AssertionType] = JwtBearer };
        Mutate(fields, scenario);
        var error = AdministratorTransportGuard.Error(fields, OpenIddictServerEndpointType.Token);
        Assert.Equal(expected, error is null);
        Assert.Equal(error, AdministratorTransportGuard.Error(fields, OpenIddictServerEndpointType.PushedAuthorization));
        if (!expected)
        {
            Assert.Equal(scenario is AdministratorTransportCase.MissingClient or AdministratorTransportCase.WrongClient or
                AdministratorTransportCase.MissingAssertion or AdministratorTransportCase.EmptyAssertion or AdministratorTransportCase.WrongAssertionType
                    ? "invalid_client" : "invalid_request", error);
        }
    }

    /// <summary>Authorization is a PAR continuation and must not carry a second client proof, even an empty-present proof field.</summary>
    [Fact]
    public void Given_AuthorizationContinuation_When_ClientProofIsPresent_Then_RequestIsDenied()
    {
        var fields = new Dictionary<string, StringValues> { [ClientId] = Client, [RequestUri] = Reference };
        Assert.Null(AdministratorTransportGuard.Error(fields, OpenIddictServerEndpointType.Authorization));
        fields[Assertion] = string.Empty;
        Assert.Equal("invalid_request", AdministratorTransportGuard.Error(fields, OpenIddictServerEndpointType.Authorization));
    }

    /// <summary>Introduces independent unsafe shapes without changing valid control fields.</summary>
    private static void Mutate(Dictionary<string, StringValues> fields, AdministratorTransportCase scenario)
    {
        switch (scenario)
        {
            case AdministratorTransportCase.MissingClient:
                fields.Remove(ClientId);
                break;
            case AdministratorTransportCase.WrongClient:
                fields[ClientId] = PilotClient;
                break;
            case AdministratorTransportCase.DuplicateClient:
                fields[ClientId] = new StringValues([Client, Client]);
                break;
            case AdministratorTransportCase.DuplicateOtherField:
                fields[Code] = new StringValues([SyntheticProof, SyntheticProof]);
                break;
            case AdministratorTransportCase.AliasedClient:
                fields.Remove(ClientId);
                fields[AliasedClient] = Client;
                break;
            case AdministratorTransportCase.AliasedAssertion:
                fields[AliasedAssertion] = SyntheticProof;
                break;
            case AdministratorTransportCase.EmptySecret:
                fields[Secret] = string.Empty;
                break;
            case AdministratorTransportCase.EmptyResource:
                fields[Resource] = string.Empty;
                break;
            case AdministratorTransportCase.EmptyAudience:
                fields[Audience] = string.Empty;
                break;
            case AdministratorTransportCase.MissingAssertion:
                fields.Remove(Assertion);
                break;
            case AdministratorTransportCase.EmptyAssertion:
                fields[Assertion] = string.Empty;
                break;
            case AdministratorTransportCase.WrongAssertionType:
                fields[AssertionType] = SyntheticProof;
                break;
        }
    }
}

/// <summary>Names independent raw request field controls.</summary>
public enum AdministratorTransportCase
{
    Valid, MissingClient, WrongClient, DuplicateClient, DuplicateOtherField, AliasedClient, AliasedAssertion, EmptySecret,
    EmptyResource, EmptyAudience, MissingAssertion, EmptyAssertion, WrongAssertionType
}
