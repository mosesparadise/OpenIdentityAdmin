using System.Globalization;
using System.Security.Claims;
using IdentityPlatform.Infrastructure.Features.Identity.BrowserCompletion;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using OpenIddict.Abstractions;

namespace IdentityPlatform.AuthServer.Features.Protocol.Administrator;

/// <summary>Owns exact private request provenance and the two ID-only authenticated browser bindings.</summary>
internal static class BrowserIssuanceClaims
{
    internal const string RequestId = "oi_b3_request_id";
    internal const string SourceExpiry = "admin_session_expires_at";
    internal const string Intent = "admin_login_intent";
    private const int MultiplicityProbe = 2;

    /// <summary>Reads only one canonical scalar originating native request ID, never the later code token ID.</summary>
    internal static string? Origin(ClaimsPrincipal principal) => Single(principal, RequestId) is { ValueType: ClaimValueTypes.String } claim &&
        claim.Value.Length is > 0 and <= IssuerContinuationPolicy.ReferenceLength && claim.GetDestinations().IsEmpty ? claim.Value : null;

    /// <summary>Detects orphaned or aliased originating-request provenance, including empty and malformed values.</summary>
    internal static bool OriginAbsent(ClaimsPrincipal principal) => !principal.Claims.Any(claim =>
        string.Equals(claim.Type, RequestId, StringComparison.OrdinalIgnoreCase));

    /// <summary>Requires unchanged private values and empty public destinations on all browser transport/provenance claims.</summary>
    internal static bool PrivateMatches(ClaimsPrincipal principal, BrowserLoginBinding binding, string origin) =>
        binding.Matches(principal) && Origin(principal) == origin && principal.Claims
            .Where(claim => claim.Type is BrowserLoginBinding.IntentClaim or BrowserLoginBinding.CompletionClaim)
            .All(claim => claim.GetDestinations().IsEmpty);

    /// <summary>Publishes only authenticated source data at exchange; the native serializer retains numeric JSON typing.</summary>
    internal static void Publish(ClaimsPrincipal principal, long expires, string intent)
    {
        principal.SetClaim(SourceExpiry, expires);
        principal.SetClaim(Intent, intent);
        foreach (var claim in principal.Claims.Where(claim => claim.Type is SourceExpiry or Intent))
        {
            claim.SetDestinations(OpenIddictConstants.Destinations.IdentityToken);
        }
    }

    /// <summary>Checks exact scalar values/types independently of token preparation and public-claim fingerprints.</summary>
    internal static bool PublicMatches(ClaimsPrincipal principal, long expires, string intent) =>
        Single(principal, SourceExpiry) is { ValueType: ClaimValueTypes.Integer64 } deadline &&
        deadline.Value == expires.ToString(CultureInfo.InvariantCulture) &&
        Single(principal, Intent) is { ValueType: ClaimValueTypes.String } digest && digest.Value == intent;

    /// <summary>Detects case-aliased and malformed presence instead of permitting a silent downgrade to ordinary B2.</summary>
    internal static bool PublicAbsent(ClaimsPrincipal principal) => !principal.Claims.Any(claim =>
        string.Equals(claim.Type, SourceExpiry, StringComparison.OrdinalIgnoreCase) || string.Equals(claim.Type, Intent, StringComparison.OrdinalIgnoreCase));

    /// <summary>Detects all private B3 representation, including orphaned originating-request fields.</summary>
    internal static bool PrivateAbsent(ClaimsPrincipal principal) => BrowserLoginBinding.Read(principal, out _) == BrowserLoginBindingState.Absent &&
        OriginAbsent(principal);

    /// <summary>Requires original ordinal spelling and multiplicity before accepting any value.</summary>
    private static Claim? Single(ClaimsPrincipal principal, string type) => principal.Claims
        .Where(claim => string.Equals(claim.Type, type, StringComparison.OrdinalIgnoreCase)).Take(MultiplicityProbe).ToArray() is [var claim] &&
        claim.Type == type ? claim : null;
}
