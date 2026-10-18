using System.Security.Claims;
using System.Text.Json;
using IdentityPlatform.AuthServer.Features.Protocol.Administrator;
using Microsoft.Extensions.Logging.Abstractions;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace IdentityPlatform.Host.Tests.Features.ProtocolStorage;

/// <summary>Rejects pre-formatter field injection and mismatched native bindings independently of frozen response bytes.</summary>
public sealed class NativeAccountResponseTests
{
    private const string Issuer = "https://issuer.example.test/";
    private const string Code = "synthetic-code";
    private const string Access = "synthetic-access";
    private const string Identity = "synthetic-identity";
    private const string State = "synthetic-state";
    private const string Changed = "changed";
    private const string Scopes = "openid identity.admin";
    private const int LifetimeSeconds = 60;

    /// <summary>Both endpoint profiles accept their exact native shape and reject every added credential or identity field.</summary>
    [Theory]
    [InlineData(false, null)]
    [InlineData(true, null)]
    [InlineData(false, "refresh_token")]
    [InlineData(true, "refresh_token")]
    [InlineData(false, "email")]
    [InlineData(true, "email")]
    [InlineData(false, "role")]
    [InlineData(true, "role")]
    [InlineData(false, "request_uri")]
    [InlineData(true, "request_uri")]
    public void Given_NativeSuccessFields_When_AdditionalFieldAppearsBeforeFormatting_Then_OnlyExactShapeQualifies(bool authorization, string? extra)
    {
        var context = Create(authorization);
        Assert.True(NativeAccountResponse.Allows(context));
        if (extra is not null)
        {
            context.Response.SetParameter(extra, Changed);
        }
        Assert.Equal(extra is null, NativeAccountResponse.Allows(context));
    }

    /// <summary>Required scalar bindings cannot be changed, removed or replaced with an equivalent-looking JSON array.</summary>
    [Theory]
    [InlineData(true, "code")]
    [InlineData(true, "state")]
    [InlineData(true, "iss")]
    [InlineData(false, "access_token")]
    [InlineData(false, "id_token")]
    [InlineData(false, "token_type")]
    [InlineData(false, "scope")]
    public void Given_ExpectedBinding_When_ChangedMissingOrArrayValued_Then_SuccessIsRejected(bool authorization, string name)
    {
        var context = Create(authorization);
        Assert.True(NativeAccountResponse.Allows(context));
        var original = (string)context.Response[name]!;
        context.Response.SetParameter(name, Changed);
        Assert.False(NativeAccountResponse.Allows(context));
        context.Response.RemoveParameter(name);
        Assert.False(NativeAccountResponse.Allows(context));
        context.Response.SetParameter(name, JsonSerializer.SerializeToElement(new[] { original }));
        Assert.False(NativeAccountResponse.Allows(context));
    }

    /// <summary>Token metadata must be numeric, positive and no longer than the actual prepared credential's lifetime.</summary>
    [Theory]
    [InlineData("0")]
    [InlineData("61")]
    [InlineData("300")]
    [InlineData("1.5")]
    [InlineData("\"60\"")]
    public void Given_NativeAccessLifetime_When_ResponseMetadataWidensOrChangesType_Then_IssuanceIsRejected(string lifetime)
    {
        var context = Create(false);
        Assert.True(NativeAccountResponse.Allows(context));
        context.Response.SetParameter(OpenIddictConstants.Parameters.ExpiresIn, JsonSerializer.Deserialize<JsonElement>(lifetime));
        Assert.False(NativeAccountResponse.Allows(context));
    }

    /// <summary>Creates finite synthetic response bindings only; this helper grants no native generation or publication capability.</summary>
    private static OpenIddictServerEvents.ProcessSignInContext Create(bool authorization)
    {
        var context = new OpenIddictServerEvents.ProcessSignInContext(new()
        {
            Logger = NullLogger.Instance,
            Options = new()
            {
                Issuer = new Uri(Issuer)
            },
            EndpointType = authorization ? OpenIddictServerEndpointType.Authorization : OpenIddictServerEndpointType.Token,
            Request = new()
            {
                State = State
            },
            Response = new()
        })
        {
            AuthorizationCode = Code,
            AccessToken = Access,
            IdentityToken = Identity
        };
        if (authorization)
        {
            context.Response.Code = Code;
            context.Response.State = State;
            context.Response.Iss = Issuer;
        }
        else
        {
            context.Response.AccessToken = Access;
            context.Response.IdToken = Identity;
            context.Response.TokenType = OpenIddictConstants.TokenTypes.Bearer;
            context.Response.ExpiresIn = LifetimeSeconds;
            context.Response.Scope = Scopes;
            context.AccessTokenPrincipal = new ClaimsPrincipal(new ClaimsIdentity());
            context.AccessTokenPrincipal.SetCreationDate(DateTimeOffset.UnixEpoch);
            context.AccessTokenPrincipal.SetExpirationDate(DateTimeOffset.UnixEpoch.AddSeconds(LifetimeSeconds));
        }
        return context;
    }
}
