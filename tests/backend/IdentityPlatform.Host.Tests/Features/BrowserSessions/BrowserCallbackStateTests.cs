using System.Security.Claims;
using IdentityPlatform.Admin.Bff.Features.BrowserSessions;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Host.Tests.Features.BrowserSessions;

/// <summary>Exercises exact protected callback provenance independently of native signature and durable consumption tests.</summary>
public sealed class BrowserCallbackStateTests
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
}
