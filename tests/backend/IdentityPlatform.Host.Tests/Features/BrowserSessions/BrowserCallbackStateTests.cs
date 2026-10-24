using System.Security.Claims;
using IdentityPlatform.Admin.Bff.Features.BrowserSessions;
using IdentityPlatform.Host.Tests.Features.AdministratorClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;
using OpenIddict.Client;

namespace IdentityPlatform.Host.Tests.Features.BrowserSessions;

/// <summary>Exercises exact protected callback provenance independently of native signature and durable consumption tests.</summary>
public sealed class BrowserCallbackStateTests(NativeClientCredentialFixture fixture) : IClassFixture<NativeClientCredentialFixture>
{
    private const string Missing = "missing";
    private const string Duplicate = "duplicate";
    private const string Alias = "alias";
    private const string WrongType = "type";
    private const string WrongGeneration = "generation";
    private const string Destination = "destination";
    private const string Target = "target";
    private const string BrowserClaim = "oi_b3_browser";
    private const string IntentClaim = "oi_b3_login_intent";
    private const string GenerationClaim = "oi_b3_generation";
    private const string ReservationClaim = "oi_b3_reservation";
    private const string AliasedReservation = "OI_B3_RESERVATION";
    private const string AliasedGeneration = "01";
    private const string UnexpectedTarget = "/unexpected";
    private const string NativeSubject = "synthetic-state-subject";
    private const int NativeLifetimeMinutes = 5;

    private static readonly string Browser = new('A', 64);
    private static readonly string Intent = new('B', 64);
    private static readonly string Reservation = new('C', 64);

    /// <summary>A valid tuple matches, but malformed presence never normalizes or downgrades into product authority.</summary>
    [Theory]
    [InlineData(Missing)]
    [InlineData(Duplicate)]
    [InlineData(Alias)]
    [InlineData(WrongType)]
    [InlineData(WrongGeneration)]
    [InlineData(Destination)]
    [InlineData(Target)]
    public void Given_ProtectedCallbackTuple_When_ProvenanceIsChanged_Then_ExactBindingDenies(string change)
    {
        var state = new ClaimsPrincipal(new ClaimsIdentity());
        state.SetClaim(BrowserClaim, Browser).SetClaim(IntentClaim, Intent)
            .SetClaim(GenerationClaim, 1L).SetClaim(ReservationClaim, Reservation);
        Assert.True(BrowserCallbackState.Matches(state, Browser, Intent, 1, Reservation));
        var identity = (ClaimsIdentity)state.Identity!;
        var original = identity.FindFirst(ReservationClaim)!;
        switch (change)
        {
            case Missing:
                identity.RemoveClaim(original);
                break;
            case Duplicate:
                identity.AddClaim(original.Clone());
                break;
            case Alias:
                identity.AddClaim(new(AliasedReservation, Reservation));
                break;
            case WrongType:
                identity.RemoveClaim(original);
                identity.AddClaim(new(original.Type, Reservation, ClaimValueTypes.Integer64));
                break;
            case WrongGeneration:
                state.SetClaim(GenerationClaim, AliasedGeneration);
                break;
            case Destination:
                original.SetDestinations(OpenIddictConstants.Destinations.IdentityToken);
                break;
            case Target:
                state.SetClaim(OpenIddictConstants.Claims.TargetLinkUri, UnexpectedTarget);
                break;
        }
        Assert.False(BrowserCallbackState.Matches(state, Browser, Intent, 1, Reservation));
    }

    /// <summary>Native signing, encryption and validation preserve numeric provenance across both JSON integer representations.</summary>
    [Fact]
    public async Task Given_ProtectedGenerations_When_NativeStateRoundTrips_Then_CanonicalIntegerWidthsRemainBound()
    {
        using var credentials = NativeClientProfileTests.Load(fixture);
        await using var services = NativeClientCompositionTests.Create(credentials);
        var options = services.GetRequiredService<IOptionsMonitor<OpenIddictClientOptions>>().CurrentValue;
        foreach (var generation in new long[] { 1, int.MaxValue, (long)int.MaxValue + 1, long.MaxValue })
        {
            var now = DateTimeOffset.UtcNow;
            var state = new ClaimsPrincipal(new ClaimsIdentity()).SetClaim(OpenIddictConstants.Claims.Subject, NativeSubject)
                .SetCreationDate(now).SetExpirationDate(now.AddMinutes(NativeLifetimeMinutes))
                .SetClaim(OpenIddictConstants.Claims.Private.Issuer, NativeClientProfileTests.Client)
                .SetClaim(BrowserClaim, Browser).SetClaim(IntentClaim, Intent)
                .SetClaim(GenerationClaim, generation).SetClaim(ReservationClaim, Reservation);
            var encoded = await NativeClientRetainedKeyTests.GenerateAsync(services, credentials, state);
            var decoded = await NativeClientRetainedKeyTests.ValidateAsync(options, encoded);
            Assert.Equal(generation <= int.MaxValue ? ClaimValueTypes.Integer32 : ClaimValueTypes.Integer64, decoded.FindFirst(GenerationClaim)!.ValueType);
            Assert.True(BrowserCallbackState.Matches(decoded, Browser, Intent, generation, Reservation));
            decoded.SetClaim(GenerationClaim, generation.ToString(System.Globalization.CultureInfo.InvariantCulture));
            Assert.False(BrowserCallbackState.Matches(decoded, Browser, Intent, generation, Reservation));
        }
    }
}
