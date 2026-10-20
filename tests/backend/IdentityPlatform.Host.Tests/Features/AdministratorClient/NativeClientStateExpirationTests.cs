using System.Security.Claims;
using IdentityPlatform.Admin.Bff.Features.Authentication.Protocol;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using OpenIddict.Client;

namespace IdentityPlatform.Host.Tests.Features.AdministratorClient;

/// <summary>Restricts deferred lifetime denial to the exact fixed callback state while preserving native expiration for every other token.</summary>
public sealed class NativeClientStateExpirationTests
{
    private const string Registration = "identity-admin-bff";
    private const string OtherRegistration = "synthetic-other-client";
    private const int ExpiredSeconds = 1;

    /// <summary>Expired fixed state proceeds only to durable redemption; identity tokens and another registration retain actual native rejection.</summary>
    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public async Task Given_ExpiredNativeToken_When_LifetimeBoundaryRuns_Then_OnlyFixedStateDefersDenialForConsumption(bool state, bool registered)
    {
        var type = state ? OpenIddictConstants.TokenTypeIdentifiers.Private.StateToken : OpenIddictConstants.TokenTypeIdentifiers.IdentityToken;
        var context = new OpenIddictClientEvents.ValidateTokenContext(new OpenIddictClientTransaction
        {
            Options = new() { TimeProvider = TimeProvider.System },
            Logger = NullLogger.Instance
        })
        {
            Principal = new ClaimsPrincipal(new ClaimsIdentity()).SetTokenType(type)
                .SetExpirationDate(DateTimeOffset.UtcNow.AddSeconds(-ExpiredSeconds))
                .SetClaim(OpenIddictConstants.Claims.Private.RegistrationId, registered ? Registration : OtherRegistration),
            TokenValidationParameters = new TokenValidationParameters { ClockSkew = TimeSpan.Zero }
        };
        context.ValidTokenTypes.Add(type);
        await new NativeClientStateExpiration(new OpenIddictClientHandlers.Protection.ValidateExpirationDate()).HandleAsync(context);
        Assert.Equal(!(state && registered), context.IsRejected);
    }
}
