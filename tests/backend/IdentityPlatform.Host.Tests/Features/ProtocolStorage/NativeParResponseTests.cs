using System.Text.Json;
using IdentityPlatform.AuthServer.Features.Protocol.Administrator;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Host.Tests.Features.ProtocolStorage;

/// <summary>Constrains native PAR output to its two finite protocol fields before an issuance can commit.</summary>
public sealed class NativeParResponseTests
{
    private const string Reference = "urn:ietf:params:oauth:request_uri:synthetic-reference";
    private const int AcceptedLifetime = 60;
    private const string ExtraCredential = "unexpected-credential";

    /// <summary>Only positive numeric integral lifetimes within the approved sixty-second ceiling qualify.</summary>
    [Theory]
    [InlineData("1", true)]
    [InlineData("60", true)]
    [InlineData("0", false)]
    [InlineData("61", false)]
    [InlineData("-1", false)]
    [InlineData("1.5", false)]
    [InlineData("\"60\"", false)]
    [InlineData("[60]", false)]
    [InlineData("null", false)]
    [InlineData("true", false)]
    public void Given_NativeLifetime_When_ResponseChecked_Then_OnlyBoundedIntegerAccepted(string lifetime, bool accepted)
    {
        var response = new OpenIddictResponse { RequestUri = Reference };
        response.SetParameter(OpenIddictConstants.Parameters.ExpiresIn, JsonSerializer.Deserialize<JsonElement>(lifetime));
        Assert.Equal(accepted, NativeParResponse.Allows(response));
    }

    /// <summary>Even correctly typed native reference/lifetime fields cannot authorize additional released credentials.</summary>
    [Theory]
    [InlineData(OpenIddictConstants.Parameters.AccessToken)]
    [InlineData(OpenIddictConstants.Parameters.Code)]
    [InlineData(OpenIddictConstants.Parameters.IdToken)]
    [InlineData(OpenIddictConstants.Parameters.RefreshToken)]
    public void Given_ExtraCredential_When_ResponseChecked_Then_IssuanceDenied(string parameter)
    {
        var response = new OpenIddictResponse { RequestUri = Reference, ExpiresIn = AcceptedLifetime };
        response.SetParameter(parameter, ExtraCredential);
        Assert.False(NativeParResponse.Allows(response));
    }
}
