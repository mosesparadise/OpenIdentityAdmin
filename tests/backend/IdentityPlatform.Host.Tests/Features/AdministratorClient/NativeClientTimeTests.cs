using System.Security.Claims;
using IdentityPlatform.Admin.Bff.Features.Authentication.Protocol;
using OpenIddict.Abstractions;
using OpenIddict.Client;

namespace IdentityPlatform.Host.Tests.Features.AdministratorClient;

/// <summary>Checks strict identity-token and whole callback deadlines without bypassing earlier native consumption.</summary>
public sealed class NativeClientTimeTests(NativeClientCredentialFixture fixture) : IClassFixture<NativeClientCredentialFixture>
{
    private const int LifetimeSeconds = 300;
    private const int FreshAgeSeconds = 299;
    private const string AdministratorScope = "identity.admin";
    private const string Registration = "identity-admin-bff";
    private static readonly DateTimeOffset Now = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Equality is expired and neither future issuance nor an overlong interval can extend the five-minute bound.</summary>
    [Theory]
    [InlineData(-299, 1, true)]
    [InlineData(-300, 0, false)]
    [InlineData(-301, -1, false)]
    [InlineData(-300, 1, false)]
    [InlineData(30, 300, true)]
    [InlineData(31, 300, false)]
    [InlineData(1, 1, false)]
    public async Task Given_NativeIdentityTokenDates_When_StrictPolicyRuns_Then_ExactDeadlineAndLifetimeLimitsApply(int created, int expires, bool valid)
    {
        var context = new OpenIddictClientEvents.ValidateTokenContext(Transaction()) { Principal = Principal(created, expires) };
        context.ValidTokenTypes.Add(OpenIddictConstants.TokenTypeIdentifiers.IdentityToken);
        await new NativeClientTokenTime().HandleAsync(context);
        Assert.Equal(!valid, context.IsRejected);
    }

    /// <summary>State expiry remains for durable redemption so an expired but cryptographically valid callback can still be consumed.</summary>
    [Fact]
    public async Task Given_ExpiredNativeState_When_IdentityTokenTimeGuardRuns_Then_DurableConsumptionIsNotPreempted()
    {
        var context = new OpenIddictClientEvents.ValidateTokenContext(Transaction()) { Principal = Principal(-LifetimeSeconds, 0) };
        context.ValidTokenTypes.Add(OpenIddictConstants.TokenTypeIdentifiers.Private.StateToken);
        await new NativeClientTokenTime().HandleAsync(context);
        Assert.False(context.IsRejected);
    }

    /// <summary>The final guard rechecks both independent expirations after all backchannel work, before merged-principal publication.</summary>
    [Theory]
    [InlineData(1, 1, true)]
    [InlineData(0, 1, false)]
    [InlineData(1, 0, false)]
    [InlineData(-1, 1, false)]
    [InlineData(1, -1, false)]
    public async Task Given_CompletedNativeCallback_When_RemoteWorkCrossesDeadline_Then_NoExpiredOutputIsAccepted(int stateEnd, int identityEnd, bool valid)
    {
        const int SendSeconds = 2;
        using var credentials = NativeClientProfileTests.Load(fixture);
        var profile = NativeClientProfile.Create(NativeClientProfileTests.Issuer, NativeClientProfileTests.Client,
            NativeClientProfileTests.Callback, NativeClientProfileTests.KeyId, credentials);
        var transaction = Transaction();
        transaction.Request = new();
        var context = new OpenIddictClientEvents.ProcessAuthenticationContext(transaction)
        {
            StateTokenPrincipal = State(stateEnd + SendSeconds),
            BackchannelIdentityTokenPrincipal = Principal(-FreshAgeSeconds + SendSeconds, identityEnd + SendSeconds)
        };
        NativeClientCallback.Attach(context, profile);
        ((Clock)context.Options.TimeProvider!).Advance(TimeSpan.FromSeconds(SendSeconds));
        await new NativeClientAuthenticationCompletion().HandleAsync(context);
        Assert.Equal(!valid, context.IsRejected);
    }

    /// <summary>Provides the fixed protected bindings that actual native preparation and early redemption preserve.</summary>
    private static ClaimsPrincipal State(int expires)
    {
        const int InitialAgeSeconds = 297;
        return Principal(-InitialAgeSeconds, expires)
            .SetClaim(OpenIddictConstants.Claims.Private.RegistrationId, Registration)
            .SetClaim(OpenIddictConstants.Claims.Issuer, NativeClientProfileTests.Client)
            .SetClaim(OpenIddictConstants.Claims.AuthorizationServer, NativeClientProfileTests.Issuer)
            .SetClaim(OpenIddictConstants.Claims.Private.RedirectUri, NativeClientProfileTests.Callback)
            .SetClaim(OpenIddictConstants.Claims.Private.GrantType, OpenIddictConstants.GrantTypes.AuthorizationCode)
            .SetClaim(OpenIddictConstants.Claims.Private.ResponseType, OpenIddictConstants.ResponseTypes.Code)
            .SetScopes(OpenIddictConstants.Scopes.OpenId, AdministratorScope);
    }

    /// <summary>Uses a deterministic native clock rather than wall-time sleeps around exact expiration equality.</summary>
    private static OpenIddictClientTransaction Transaction() => new() { Options = new() { TimeProvider = new Clock() } };

    /// <summary>Uses native private date claims as produced by cryptographic validation.</summary>
    private static ClaimsPrincipal Principal(int created, int expires) => new ClaimsPrincipal(new ClaimsIdentity())
        .SetCreationDate(Now.AddSeconds(created)).SetExpirationDate(Now.AddSeconds(expires));

    /// <summary>Supplies a stable native observation instant to every boundary sample.</summary>
    private sealed class Clock : TimeProvider
    {
        private DateTimeOffset current = Now;

        /// <summary>Returns the exact current instant without wall-time sleeps.</summary>
        public override DateTimeOffset GetUtcNow() => current;

        /// <summary>Models elapsed remote work independently of the protected original state timestamps.</summary>
        internal void Advance(TimeSpan elapsed) => current += elapsed;
    }
}
