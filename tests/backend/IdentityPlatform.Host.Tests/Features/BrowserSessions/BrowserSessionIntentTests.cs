using System.Security.Claims;
using IdentityPlatform.Admin.Bff.Features.BrowserSessions;
using IdentityPlatform.Host.Tests.Features.AdministratorClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;
using OpenIddict.Client;

namespace IdentityPlatform.Host.Tests.Features.BrowserSessions;

/// <summary>Proves expected-intent survival through real native state protection, without claiming durable redemption or a browser callback.</summary>
public sealed class BrowserSessionIntentTests(NativeClientCredentialFixture fixture) : IClassFixture<NativeClientCredentialFixture>
{
    private const string NativeStateSubject = "synthetic-state-subject";
    private const int DigestLength = 64;
    private const string Missing = "missing";
    private const string Duplicate = "duplicate";
    private const string Alias = "alias";
    private const string Lowercase = "lowercase";
    private const string WrongType = "type";
    private const string AliasClaim = "OI_B3_LOGIN_INTENT";

    /// <summary>Only the restored protected-state expectation admits the signed identity; a replacement expectation cannot borrow its signature.</summary>
    [Fact]
    public async Task Given_ServerIntent_When_NativeStateIsProtectedAndValidated_Then_ExactExpectationBindsIdentity()
    {
        using var keys = NativeClientProfileTests.Load(fixture);
        await using var services = NativeClientCompositionTests.Create(keys);
        var options = services.GetRequiredService<IOptionsMonitor<OpenIddictClientOptions>>().CurrentValue;
        var challenge = new OpenIddictClientEvents.ProcessChallengeContext(new OpenIddictClientTransaction
        {
            Options = options,
            Registration = Assert.Single(options.Registrations),
            Logger = NullLogger.Instance,
            Request = new()
        })
        {
            Principal = new ClaimsPrincipal(new ClaimsIdentity([new(BrowserSessionIntent.ClaimType, BrowserSessionToken.Intent), new(OpenIddictConstants.Claims.Subject, NativeStateSubject)])),
            GrantType = OpenIddictConstants.GrantTypes.AuthorizationCode,
            ResponseType = OpenIddictConstants.ResponseTypes.Code,
            RedirectUri = NativeClientProfileTests.Callback
        };
        await new OpenIddictClientHandlers.PrepareLoginStateTokenPrincipal().HandleAsync(challenge);
        var encoded = await NativeClientRetainedKeyTests.GenerateAsync(services, keys, challenge.StateTokenPrincipal!);
        var state = await NativeClientRetainedKeyTests.ValidateAsync(options, encoded);
        Assert.Equal(BrowserSessionToken.Intent, BrowserSessionIntent.Read(state));
        var identity = await BrowserSessionToken.CreateAsync(fixture);
        Assert.NotNull(BrowserSessionIdentity.Validate(identity.Principal, identity.Encoded, BrowserSessionIntent.Read(state)!, identity.Now));
        state.SetClaim(BrowserSessionIntent.ClaimType, new string('0', DigestLength));
        Assert.Null(BrowserSessionIdentity.Validate(identity.Principal, identity.Encoded, BrowserSessionIntent.Read(state)!, identity.Now));
    }

    /// <summary>Ambiguous or noncanonical protected expectations cannot be normalized into a browser binding.</summary>
    [Theory]
    [InlineData(Missing)]
    [InlineData(Duplicate)]
    [InlineData(Alias)]
    [InlineData(Lowercase)]
    [InlineData(WrongType)]
    public void Given_ProtectedExpectation_When_Malformed_Then_Denied(string fault)
    {
        var identity = new ClaimsIdentity([new(BrowserSessionIntent.ClaimType, BrowserSessionToken.Intent)]);
        var state = new ClaimsPrincipal(identity);
        Assert.Equal(BrowserSessionToken.Intent, BrowserSessionIntent.Read(state));
        if (fault == Missing)
        {
            identity.RemoveClaim(Assert.Single(identity.Claims));
        }
        if (fault == Duplicate)
        {
            identity.AddClaim(new(BrowserSessionIntent.ClaimType, BrowserSessionToken.Intent));
        }
        if (fault == Alias)
        {
            identity.AddClaim(new(AliasClaim, BrowserSessionToken.Intent));
        }
        if (fault == Lowercase)
        {
            state.SetClaim(BrowserSessionIntent.ClaimType, BrowserSessionToken.Intent.ToLowerInvariant());
        }
        if (fault == WrongType)
        {
            identity.RemoveClaim(Assert.Single(identity.Claims));
            identity.AddClaim(new(BrowserSessionIntent.ClaimType, BrowserSessionToken.Intent, ClaimValueTypes.Integer));
        }
        Assert.Null(BrowserSessionIntent.Read(state));
    }
}
