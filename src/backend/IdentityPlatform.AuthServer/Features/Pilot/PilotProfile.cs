using System.Collections.Immutable;
using OpenIddict.Abstractions;

namespace IdentityPlatform.AuthServer.Features.Pilot;

public static class PilotProfile
{
    public const string Id = "first-party-v1";
    public const string ClientId = "identityplatform-pilot";
    public const string Scope = "pilot_api";
    public const string Resource = "identityplatform-pilot-api";
    public const string SessionClaim = "oi_pilot_session";
    public const string SessionCookie = "__Host-identity-pilot";
    public const string CorrelationPrefix = "__Host-pilot-flow-";
    public const string BudgetCookie = "__Host-pilot-budget";
    public const int MaximumInteractions = 8;
    public const int InteractionSeconds = 300;
    public const int SessionSeconds = 900;

    public static ImmutableHashSet<string> Permissions
    {
        get;
    } = ImmutableHashSet.Create(StringComparer.Ordinal,
        OpenIddictConstants.Permissions.Endpoints.Authorization,
        OpenIddictConstants.Permissions.Endpoints.Token,
        OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode,
        OpenIddictConstants.Permissions.ResponseTypes.Code,
        OpenIddictConstants.Permissions.Prefixes.Scope + Scope);

    public static ImmutableHashSet<string> Requirements
    {
        get;
    } = ImmutableHashSet.Create(StringComparer.Ordinal,
        OpenIddictConstants.Requirements.Features.ProofKeyForCodeExchange);
}
