using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using IdentityPlatform.Infrastructure.Features.Identity.ManagedApplications;
using OpenIddict.Abstractions;

namespace IdentityPlatform.AuthServer.Features.Protocol.ManagedApplications;

/// <summary>Constructs exactly consent-bound managed principals and admits only the native private code metadata required for exchange.</summary>
internal static class ManagedPrincipal
{
    private const string AuthenticationType = "managed-web";
    private const int Multiplicity = 1;
    private const int SubjectDestinations = 2;
    private static readonly HashSet<string> NativeClaims = new(StringComparer.Ordinal)
    {
        OpenIddictConstants.Claims.Subject, ManagedDemand.SelectorClaim, ManagedDemand.RevisionClaim, ManagedDemand.PolicyClaim,
        OpenIddictConstants.Claims.Private.Resource,
        OpenIddictConstants.Claims.Private.AuthorizationId, OpenIddictConstants.Claims.Private.CodeChallenge,
        OpenIddictConstants.Claims.Private.CodeChallengeMethod, OpenIddictConstants.Claims.Private.CreationDate,
        OpenIddictConstants.Claims.Private.ExpirationDate, OpenIddictConstants.Claims.Private.Issuer,
        OpenIddictConstants.Claims.Private.Nonce, OpenIddictConstants.Claims.Private.Presenter,
        OpenIddictConstants.Claims.Private.RedirectUri, OpenIddictConstants.Claims.Private.Scope,
        OpenIddictConstants.Claims.Private.TokenId, OpenIddictConstants.Claims.Private.TokenType,
        OpenIddictConstants.Claims.JwtId
    };

    /// <summary>Projects only the bound consent decision; email is read separately from current Identity state at exchange.</summary>
    internal static ClaimsPrincipal Create(ManagedLoginInteraction row, string client, string selector)
    {
        var decision = ManagedScopeBinding.Read(row);
        return Create(row.UserId, client, selector, row.ApplicationRevision.ToString(CultureInfo.InvariantCulture), row.PolicyDigest,
            decision.Granted, decision.Resources);
    }

    /// <summary>Copies narrow authenticated code provenance provisionally; locked issuance must independently match the current interaction.</summary>
    internal static ClaimsPrincipal Exchange(ClaimsPrincipal original) => Create(original.GetClaim(OpenIddictConstants.Claims.Subject),
        original.GetPresenters().Single(), original.GetClaim(ManagedDemand.SelectorClaim)!, original.GetClaim(ManagedDemand.RevisionClaim)!,
        original.GetClaim(ManagedDemand.PolicyClaim), original.GetScopes(), original.GetResources());

    /// <summary>Creates a fresh managed identity with finite destinations and no copied account or administrator principal.</summary>
    private static ClaimsPrincipal Create(string? subject, string client, string selector, string revision, string? digest,
        System.Collections.Immutable.ImmutableArray<string> scopes, System.Collections.Immutable.ImmutableArray<string> resources)
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(AuthenticationType));
        principal.SetClaim(OpenIddictConstants.Claims.Subject, subject);
        principal.SetClaim(ManagedDemand.SelectorClaim, selector);
        principal.SetClaim(ManagedDemand.RevisionClaim, revision);
        principal.SetClaim(ManagedDemand.PolicyClaim, digest);
        principal.SetScopes(scopes);
        principal.SetResources(resources);
        principal.SetPresenters(client);
        principal.SetDestinations(claim => claim.Type == OpenIddictConstants.Claims.Subject
            ? [OpenIddictConstants.Destinations.AccessToken, OpenIddictConstants.Destinations.IdentityToken] : []);
        return principal;
    }

    /// <summary>Rejects extra public/private provenance after native internal-claim restoration and binds every permitted managed value.</summary>
    internal static bool Matches(ClaimsPrincipal principal, ManagedLoginInteraction row, string client, string selector, bool protectedCode = false, ManagedEmailProjection? email = null) =>
        principal.Identities.Count() == Multiplicity && principal.Identity is ClaimsIdentity { IsAuthenticated: true, Actor: null } &&
        principal.Claims.All(claim => NativeClaims.Contains(claim.Type) || ManagedEmailClaims.Allows(claim, email) || Destinations(claim) || protectedCode && Metadata(claim)) &&
        MultiplicityValid(principal) &&
        principal.GetClaim(OpenIddictConstants.Claims.Subject) == row.UserId && principal.GetClaim(ManagedDemand.SelectorClaim) == selector &&
        principal.GetClaim(ManagedDemand.RevisionClaim) == row.ApplicationRevision.ToString(CultureInfo.InvariantCulture) &&
        ManagedScopeBinding.MatchesDigest(row, principal.GetClaim(ManagedDemand.PolicyClaim)) &&
        principal.GetScopes().SequenceEqual(ManagedScopeBinding.Read(row).Granted, StringComparer.Ordinal) &&
        principal.GetPresenters().SequenceEqual([client], StringComparer.Ordinal) && principal.GetResources().SequenceEqual(ManagedScopeBinding.Read(row).Resources, StringComparer.Ordinal) &&
        ManagedEmailClaims.Matches(principal, email, destinations: true) &&
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

    /// <summary>Allows native repeated scope members only when each is an exact distinct string; every other claim remains single-valued.</summary>
    internal static bool MultiplicityValid(ClaimsPrincipal principal) => principal.Claims.GroupBy(claim => claim.Type, StringComparer.OrdinalIgnoreCase)
        .All(group => group.Key == OpenIddictConstants.Claims.Private.Scope
            ? group.All(claim => claim.Type == group.Key && claim.ValueType == ClaimValueTypes.String) &&
                group.Select(claim => claim.Value).Distinct(StringComparer.Ordinal).Count() == group.Count()
            : group.Count() == Multiplicity);

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
