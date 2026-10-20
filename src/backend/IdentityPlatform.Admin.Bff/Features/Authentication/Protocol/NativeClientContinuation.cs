using IdentityPlatform.Admin.Bff.Features.Authentication.Ownership;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using OpenIddict.Abstractions;
using OpenIddict.Client;

namespace IdentityPlatform.Admin.Bff.Features.Authentication.Protocol;

/// <summary>Binds later PAR processing to the exact state and challenge released by confirmed native state persistence.</summary>
internal sealed class NativeClientContinuation
{
    private static readonly string Key = Guid.NewGuid().ToString();
    private readonly OpenIddictClientEvents.ProcessChallengeContext context;
    private readonly NativeClientProfile profile;
    private readonly string claims;
    private readonly string request;
    private readonly string state;

    /// <summary>Captures only committed native state and digest bindings, never a printable token or verifier snapshot.</summary>
    private NativeClientContinuation(OpenIddictClientEvents.ProcessChallengeContext context, NativeClientProfile profile)
    {
        this.context = context;
        this.profile = profile;
        NativeClientChallenge.Require(context, profile);
        if (!context.GenerateStateToken || !context.IncludeStateToken || context.StateTokenPrincipal is null || string.IsNullOrEmpty(context.StateToken))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        claims = NativeStateBinding.Claims(context.StateTokenPrincipal);
        state = FactorBinding.Digest(context.StateToken);
        request = Digest(Expected(context));
    }

    /// <summary>Attaches only from the durable state handler after confirmed commit and actual child disposal.</summary>
    internal static void Attach(OpenIddictClientEvents.ProcessChallengeContext context, NativeClientProfile profile)
    {
        if (context.Transaction.GetProperty<NativeClientContinuation>(Key) is not null)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        context.Transaction.SetProperty(Key, new NativeClientContinuation(context, profile));
    }

    /// <summary>Requires a proof created by actual durable state completion, rather than accepting a caller-supplied state string.</summary>
    internal static NativeClientContinuation Find(OpenIddictClientEvents.ProcessChallengeContext context) =>
        context.Transaction.GetProperty<NativeClientContinuation>(Key) ?? throw ProtocolStorageOwner.Unavailable();

    /// <summary>Revalidates frozen state, exact final challenge fields and native negotiated PAR settings immediately around remote transport.</summary>
    internal void Require(OpenIddictClientEvents.ProcessChallengeContext current)
    {
        NativeClientChallenge.Require(current, profile);
        (NativeClientAssertion.Find(current.Transaction) ?? throw ProtocolStorageOwner.Unavailable())
            .RequireOutput(current.ClientAssertion, current.ClientAssertionPrincipal);
        if (!ReferenceEquals(current, context) || current.StateTokenPrincipal is null || !current.IncludeStateToken || !current.GenerateStateToken ||
            !DurableClientState.IsCurrent(current.StateTokenPrincipal, current.Options.TimeProvider!) ||
            claims != NativeStateBinding.Claims(current.StateTokenPrincipal) || state != FactorBinding.Digest(current.StateToken) ||
            request != Digest(Expected(current)) || request != Digest(current.Request) || !current.SendPushedAuthorizationRequest ||
            current.PushedAuthorizationEndpoint?.OriginalString != profile.ParEndpoint.AbsoluteUri ||
            current.PushedAuthorizationEndpointClientAuthenticationMethod != OpenIddictConstants.ClientAuthenticationMethods.PrivateKeyJwt ||
            current.PushedAuthorizationRequest is null || request != Digest(current.PushedAuthorizationRequest, omitAssertion: true) ||
            !current.GenerateClientAssertion || !current.IncludeClientAssertion || string.IsNullOrEmpty(current.ClientAssertion) ||
            current.PushedAuthorizationRequest.ClientAssertion != current.ClientAssertion ||
            current.PushedAuthorizationRequest.ClientAssertionType != OpenIddictConstants.ClientAssertionTypes.JwtBearer)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
    }

    /// <summary>Builds only the fixed authorization request that native attachment is allowed to produce.</summary>
    private static OpenIddictRequest Expected(OpenIddictClientEvents.ProcessChallengeContext context)
    {
        var expected = new OpenIddictRequest
        {
            ClientId = context.ClientId,
            RedirectUri = context.RedirectUri,
            ResponseType = context.ResponseType,
            ResponseMode = context.ResponseMode,
            Scope = string.Join(' ', context.Scopes),
            Nonce = NativeClientChallenge.Hash(context.Nonce!),
            CodeChallenge = context.CodeChallenge,
            CodeChallengeMethod = context.CodeChallengeMethod,
            State = context.StateToken
        };
        foreach (var pair in context.Parameters)
        {
            expected.SetParameter(pair.Key, pair.Value);
        }
        return expected;
    }

    /// <summary>Hashes canonical parameter names and native raw values, excluding only separately bound PAR client assertion fields.</summary>
    private static string Digest(OpenIddictRequest request, bool omitAssertion = false) => FactorBinding.Digest(request.GetParameters()
        .Where(pair => !omitAssertion || pair.Key is not (OpenIddictConstants.Parameters.ClientAssertion or OpenIddictConstants.Parameters.ClientAssertionType))
        .OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => new { pair.Key, Value = pair.Value.GetRawValue() }).ToArray());
}

/// <summary>Checks committed state/profile binding immediately before and after the native PAR transport handler.</summary>
internal sealed class NativeClientParBoundary : IOpenIddictClientHandler<OpenIddictClientEvents.ProcessChallengeContext>
{
    /// <summary>Rejects late mutable context changes and state expiration without repeating any remote request.</summary>
    public ValueTask HandleAsync(OpenIddictClientEvents.ProcessChallengeContext context)
    {
        NativeClientContinuation.Find(context).Require(context);
        return ValueTask.CompletedTask;
    }
}
