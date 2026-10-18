using System.Text;
using IdentityPlatform.AuthServer.Features.Protocol.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using OpenIddict.Server;

namespace IdentityPlatform.Host.Tests.Features.AdministratorProtocol;

/// <summary>Checks the narrow legacy-routing identity without equating transport selection with client authentication.</summary>
public sealed class PilotTransportIdentityTests
{
    private const string Client = "identityplatform-pilot";
    private const string ClientId = "client_id";
    private const string BasicPrefix = "Basic ";
    private const string OtherClient = "other-client";
    private const string EncodedClient = "identityplatform%2Dpilot";
    private const string AliasedClient = "CLIENT_ID";
    private const string Assertion = "client_assertion";
    private const string SyntheticProof = "synthetic-proof";
    private const string BodySecret = "client_secret";
    private const string InvalidBasic = "Basic invalid";
    private const string Secret = ":synthetic-secret";

    /// <summary>Allows only the explicit pilot identity and rejects ambiguous/mixed proof methods before legacy transport selection.</summary>
    [Theory]
    [InlineData(PilotIdentityCase.Authorization, true)]
    [InlineData(PilotIdentityCase.BasicOnly, true)]
    [InlineData(PilotIdentityCase.BasicWithIdentity, true)]
    [InlineData(PilotIdentityCase.EncodedBasic, true)]
    [InlineData(PilotIdentityCase.NoBasic, false)]
    [InlineData(PilotIdentityCase.DifferentBasic, false)]
    [InlineData(PilotIdentityCase.DifferentForm, false)]
    [InlineData(PilotIdentityCase.DuplicateIdentity, false)]
    [InlineData(PilotIdentityCase.AliasedIdentity, false)]
    [InlineData(PilotIdentityCase.MixedAssertion, false)]
    [InlineData(PilotIdentityCase.EmptyAssertion, false)]
    [InlineData(PilotIdentityCase.BodySecret, false)]
    [InlineData(PilotIdentityCase.DuplicateHeader, false)]
    [InlineData(PilotIdentityCase.InvalidBase64, false)]
    [InlineData(PilotIdentityCase.AuthorizationWithBasic, false)]
    public void Given_RawIdentity_When_TransportIsSelected_Then_OnlyExactUnambiguousPilotUsesLegacy(PilotIdentityCase scenario, bool expected)
    {
        var fields = new Dictionary<string, StringValues> { [ClientId] = Client };
        Mutate(fields, scenario);
        var endpoint = scenario is PilotIdentityCase.Authorization or PilotIdentityCase.AuthorizationWithBasic
            ? OpenIddictServerEndpointType.Authorization : OpenIddictServerEndpointType.Token;
        StringValues header = scenario switch
        {
            PilotIdentityCase.Authorization or PilotIdentityCase.NoBasic => default,
            PilotIdentityCase.EncodedBasic => Basic(EncodedClient),
            PilotIdentityCase.DifferentBasic => Basic(OtherClient),
            PilotIdentityCase.DuplicateHeader => new StringValues([Basic(Client), Basic(Client)]),
            PilotIdentityCase.InvalidBase64 => InvalidBasic,
            _ => Basic(Client)
        };
        Assert.Equal(expected, PilotTransportIdentity.Matches(fields, header, endpoint));
    }

    /// <summary>Mutates body/query ambiguity independently of the selected transport method.</summary>
    private static void Mutate(Dictionary<string, StringValues> fields, PilotIdentityCase scenario)
    {
        switch (scenario)
        {
            case PilotIdentityCase.BasicOnly:
                fields.Clear();
                break;
            case PilotIdentityCase.DifferentForm:
                fields[ClientId] = OtherClient;
                break;
            case PilotIdentityCase.DuplicateIdentity:
                fields[ClientId] = new StringValues([Client, Client]);
                break;
            case PilotIdentityCase.AliasedIdentity:
                fields.Clear();
                fields[AliasedClient] = Client;
                break;
            case PilotIdentityCase.MixedAssertion:
                fields[Assertion] = SyntheticProof;
                break;
            case PilotIdentityCase.EmptyAssertion:
                fields[Assertion] = string.Empty;
                break;
            case PilotIdentityCase.BodySecret:
                fields[BodySecret] = string.Empty;
                break;
        }
    }

    /// <summary>Uses only native method-appropriate input; PAR can never enter legacy storage.</summary>
    [Theory]
    [InlineData("GET", OpenIddictServerEndpointType.Authorization, true)]
    [InlineData("GET", OpenIddictServerEndpointType.Token, false)]
    [InlineData("GET", OpenIddictServerEndpointType.PushedAuthorization, false)]
    [InlineData("PUT", OpenIddictServerEndpointType.Authorization, false)]
    public async Task Given_MethodAndEndpoint_When_QueryHasPilotIdentity_Then_OnlyAuthorizationGetIsSelected(string method, OpenIddictServerEndpointType endpoint, bool expected)
    {
        var http = new DefaultHttpContext();
        http.Request.Method = method;
        http.Request.QueryString = QueryString.Create(ClientId, Client);
        Assert.Equal(expected, await PilotTransportIdentity.MatchesAsync(http, endpoint));
    }

    /// <summary>Encodes synthetic transport bytes without authenticating their password.</summary>
    private static string Basic(string client) => BasicPrefix + Convert.ToBase64String(Encoding.UTF8.GetBytes(client + Secret));
}

/// <summary>Names independent raw identity/profile-routing controls.</summary>
public enum PilotIdentityCase
{
    Authorization, BasicOnly, BasicWithIdentity, EncodedBasic, NoBasic, DifferentBasic, DifferentForm, DuplicateIdentity,
    AliasedIdentity, MixedAssertion, EmptyAssertion, BodySecret, DuplicateHeader, InvalidBase64, AuthorizationWithBasic
}
