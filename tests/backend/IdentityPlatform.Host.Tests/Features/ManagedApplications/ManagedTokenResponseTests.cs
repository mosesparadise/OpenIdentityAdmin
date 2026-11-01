using System.Security.Claims;
using IdentityPlatform.AuthServer.Features.Protocol.ManagedApplications;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace IdentityPlatform.Host.Tests.Features.ManagedApplications;

/// <summary>Rejects native response metadata that overstates the actual prepared token lifetime before serialization can freeze it.</summary>
public sealed class ManagedTokenResponseTests
{
    private const string Access = "synthetic-access";
    private const string Identity = "synthetic-identity";
    private const string Bearer = "Bearer";
    private const string Scope = "openid";
    private const int PreparedSeconds = 60;

    /// <summary>The exact native lifetime ceiling is accepted; one extra second or absent prepared timestamps cannot authorize output.</summary>
    [Theory]
    [InlineData(60, false, false, true)]
    [InlineData(61, false, false, false)]
    [InlineData(60, true, false, false)]
    [InlineData(60, false, true, false)]
    public void Given_ManagedNativeResponse_When_ReportedLifetimeExceedsPreparedCredentialOrTimesAreMissing_Then_PublicationIsRejected(long seconds, bool missingCreation, bool missingExpiration, bool accepted)
    {
        var now = DateTimeOffset.UtcNow;
        var principal = new ClaimsPrincipal(new ClaimsIdentity());
        principal.SetCreationDate(missingCreation ? null : now);
        principal.SetExpirationDate(missingExpiration ? null : now.AddSeconds(PreparedSeconds));
        var transaction = new OpenIddictServerTransaction
        {
            EndpointType = OpenIddictServerEndpointType.Token,
            Response = new() { AccessToken = Access, IdToken = Identity, TokenType = Bearer, Scope = Scope, ExpiresIn = seconds }
        };
        var context = new OpenIddictServerEvents.ProcessSignInContext(transaction)
        {
            AccessToken = Access,
            IdentityToken = Identity,
            AccessTokenPrincipal = principal
        };
        Assert.Equal(accepted, ManagedResponseRules.Allows(context));
    }
}
