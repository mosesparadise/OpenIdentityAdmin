using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using IdentityPlatform.Infrastructure.Features.Identity.ManagedApplications;
using OpenIddict.Abstractions;

namespace IdentityPlatform.AuthServer.Features.Protocol.ManagedApplications;

/// <summary>Constructs identity-only managed principals and admits only the native private code metadata required for exchange.</summary>
internal static class ManagedPrincipal
{
    private const string AuthenticationType = "managed-web";
    private const int Multiplicity = 1;
    private const int SubjectDestinations = 2;
    private static readonly HashSet<string> NativeClaims = new(StringComparer.Ordinal)
    {
        OpenIddictConstants.Claims.Subject, ManagedDemand.SelectorClaim, ManagedDemand.RevisionClaim,
        OpenIddictConstants.Claims.Private.AuthorizationId, OpenIddictConstants.Claims.Private.CodeChallenge,
        OpenIddictConstants.Claims.Private.CodeChallengeMethod, OpenIddictConstants.Claims.Private.CreationDate,
        OpenIddictConstants.Claims.Private.ExpirationDate, OpenIddictConstants.Claims.Private.Issuer,
        OpenIddictConstants.Claims.Private.Nonce, OpenIddictConstants.Claims.Private.Presenter,
        OpenIddictConstants.Claims.Private.RedirectUri, OpenIddictConstants.Claims.Private.Scope,
        OpenIddictConstants.Claims.Private.TokenId, OpenIddictConstants.Claims.Private.TokenType,
        OpenIddictConstants.Claims.JwtId
    };

    /// <summary>Starts a new identity without any administrator principal, session, role, audience or permission claim.</summary>
    internal static ClaimsPrincipal Create(ManagedLoginInteraction row, string client, string selector)
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(AuthenticationType));
        principal.SetClaim(OpenIddictConstants.Claims.Subject, row.UserId);
        principal.SetClaim(ManagedDemand.SelectorClaim, selector);
        principal.SetClaim(ManagedDemand.RevisionClaim, row.ApplicationRevision.ToString(CultureInfo.InvariantCulture));
        principal.SetScopes(OpenIddictConstants.Scopes.OpenId);
        principal.SetPresenters(client);
        principal.SetDestinations(claim => claim.Type == OpenIddictConstants.Claims.Subject
            ? [OpenIddictConstants.Destinations.AccessToken, OpenIddictConstants.Destinations.IdentityToken] : []);
        return principal;
    }

    /// <summary>Rejects extra public/private provenance after native internal-claim restoration and binds every permitted managed value.</summary>
    internal static bool Matches(ClaimsPrincipal principal, ManagedLoginInteraction row, string client, string selector, bool protectedCode = false) =>
        principal.Identities.Count() == Multiplicity && principal.Identity is ClaimsIdentity { IsAuthenticated: true, Actor: null } &&
        principal.Claims.All(claim => NativeClaims.Contains(claim.Type) || Destinations(claim) || protectedCode && Metadata(claim)) &&
        principal.Claims.GroupBy(claim => claim.Type, StringComparer.OrdinalIgnoreCase).All(group => group.Count() == Multiplicity) &&
        principal.GetClaim(OpenIddictConstants.Claims.Subject) == row.UserId && principal.GetClaim(ManagedDemand.SelectorClaim) == selector &&
        principal.GetClaim(ManagedDemand.RevisionClaim) == row.ApplicationRevision.ToString(CultureInfo.InvariantCulture) &&
        principal.GetScopes().SequenceEqual([OpenIddictConstants.Scopes.OpenId], StringComparer.Ordinal) &&
        principal.GetPresenters().SequenceEqual([client], StringComparer.Ordinal) && principal.GetResources().IsEmpty &&
        principal.FindFirst(OpenIddictConstants.Claims.Subject)!.GetDestinations().ToHashSet(StringComparer.Ordinal)
            .SetEquals([OpenIddictConstants.Destinations.AccessToken, OpenIddictConstants.Destinations.IdentityToken]) &&
        principal.Claims.Where(claim => claim.Type.StartsWith(OpenIddictConstants.Claims.Prefixes.Private, StringComparison.Ordinal)).All(claim => claim.GetDestinations().IsEmpty);

    /// <summary>Accepts only the native retained subject destination map; no other identity or authority claim can gain a destination.</summary>
    private static bool Destinations(Claim claim)
    {
        if (claim.Type != OpenIddictConstants.Claims.Private.ClaimDestinationsMap)
        {
            return false;
        }
        try
        {
            using var document = JsonDocument.Parse(claim.Value);
            var root = document.RootElement;
            return root.ValueKind == JsonValueKind.Object && root.EnumerateObject().Count() == Multiplicity &&
                root.TryGetProperty(OpenIddictConstants.Claims.Subject, out var destinations) && destinations.ValueKind == JsonValueKind.Array &&
                destinations.GetArrayLength() == SubjectDestinations && destinations.EnumerateArray().All(value => value.ValueKind == JsonValueKind.String) &&
                destinations.EnumerateArray().Select(value => value.GetString()).ToHashSet(StringComparer.Ordinal)
                    .SetEquals([OpenIddictConstants.Destinations.AccessToken, OpenIddictConstants.Destinations.IdentityToken]);
        }
        catch (JsonException) { return false; }
    }

    /// <summary>Binds every restored private value to the already authenticated code, preventing mutable nonce or source substitution.</summary>
    internal static bool Restored(ClaimsPrincipal principal, ClaimsPrincipal original)
    {
        var source = original.Claims.Where(claim => claim.Type.StartsWith(OpenIddictConstants.Claims.Prefixes.Private, StringComparison.Ordinal)).ToArray();
        var restored = principal.Claims.Where(claim => claim.Type.StartsWith(OpenIddictConstants.Claims.Prefixes.Private, StringComparison.Ordinal)).ToArray();
        return source.Length == restored.Length && restored.All(claim => source.Count(value => value.Type == claim.Type &&
            value.Value == claim.Value && value.ValueType == claim.ValueType) == Multiplicity);
    }
    /// <summary>Admits standard native JWT metadata retained by real validation, without allowing it into the fresh issuance projection.</summary>
    private static bool Metadata(Claim claim) => claim.Type == OpenIddictConstants.Claims.Issuer
        ? claim.ValueType == ClaimValueTypes.String && Uri.TryCreate(claim.Value, UriKind.Absolute, out _)
        : claim.Type is OpenIddictConstants.Claims.IssuedAt or OpenIddictConstants.Claims.ExpiresAt or OpenIddictConstants.Claims.NotBefore &&
            claim.ValueType is ClaimValueTypes.Integer32 or ClaimValueTypes.Integer64 &&
            long.TryParse(claim.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds) && seconds >= 0;

}
