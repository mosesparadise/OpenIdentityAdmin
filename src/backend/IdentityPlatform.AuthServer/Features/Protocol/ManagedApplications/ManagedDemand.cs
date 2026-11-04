using IdentityPlatform.AuthServer.Features.Pilot;
using IdentityPlatform.AuthServer.Features.Protocol.Policy;
using IdentityPlatform.Core.Features.Applications;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using OpenIddict.Abstractions;

namespace IdentityPlatform.AuthServer.Features.Protocol.ManagedApplications;

/// <summary>Freezes the supported restored OpenID demand without copying administrator scopes, resources or principals.</summary>
internal sealed class ManagedDemand
{
    internal const string SelectorClaim = "oi_managed_selector";
    internal const string RevisionClaim = "oi_managed_revision";
    private const int CorrelationLength = 512;
    private readonly OpenIddictRequest request;
    internal string ClientId
    {
        get;
    }
    internal string Callback
    {
        get;
    }
    internal string Digest
    {
        get;
    }
    internal bool Cached
    {
        get;
    }

    /// <summary>Copies validated values while retaining exact native request identity for later publication checks.</summary>
    private ManagedDemand(OpenIddictRequest request)
    {
        this.request = request;
        ClientId = request.ClientId!;
        Callback = request.RedirectUri!;
        Digest = Binding(request);
        Cached = request.RequestUri is not null;
    }

    /// <summary>Reserves both fixed client names case-insensitively before selecting any managed policy.</summary>
    internal static bool Candidate(string? client) => ApplicationEditorRules.IsClientId(client) &&
        !string.Equals(client, PilotProfile.ClientId, StringComparison.OrdinalIgnoreCase) &&
        !string.Equals(client, AdministratorProtocolIdentity.ClientId, StringComparison.OrdinalIgnoreCase);

    /// <summary>Accepts only code/query/S256, exact openid and bounded state/nonce; a native request URI never grants human authority.</summary>
    internal static ManagedDemand? Read(OpenIddictRequest request) => Candidate(request.ClientId) &&
        request.ResponseType == OpenIddictConstants.ResponseTypes.Code && request.Scope == OpenIddictConstants.Scopes.OpenId &&
        request.ResponseMode is null or OpenIddictConstants.ResponseModes.Query &&
        request.Prompt is null or OpenIddictConstants.PromptValues.Login &&
        request.CodeChallengeMethod == OpenIddictConstants.CodeChallengeMethods.Sha256 && ProofKeyPolicy.IsChallenge(request.CodeChallenge) &&
        ApplicationEditorRules.IsRedirect(request.RedirectUri) && Correlation(request.State) && Correlation(request.Nonce) &&
        request.GetParameters().Keys.All(ManagedTransport.Allowed) ? new(request) : null;

    /// <summary>Detects request replacement, restored-demand mutation or changing the initial/resumed phase.</summary>
    internal bool Matches(OpenIddictRequest current) => ReferenceEquals(request, current) && Cached == (current.RequestUri is not null) &&
        Digest == Binding(current) && Read(current) is not null;

    /// <summary>Hashes only exact native-restored protocol intent; the transport reference is independently bound to its native token ID.</summary>
    internal static string Binding(OpenIddictRequest value) => FactorBinding.Digest(new[] { value.ClientId, value.RedirectUri,
        value.ResponseType, value.ResponseMode, value.Scope, value.CodeChallenge, value.CodeChallengeMethod, value.State, value.Nonce, value.Prompt });

    /// <summary>Rejects absent, oversized or control-bearing correlation values without logging their contents.</summary>
    private static bool Correlation(string? value) => ApplicationEditorRules.IsText(value, CorrelationLength);
}
