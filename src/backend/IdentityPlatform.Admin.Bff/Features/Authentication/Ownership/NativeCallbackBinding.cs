using System.Security.Claims;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using OpenIddict.Abstractions;
using OpenIddict.Client;

namespace IdentityPlatform.Admin.Bff.Features.Authentication.Ownership;

/// <summary>Freezes the validated callback before any awaited durable redemption work can change its inputs.</summary>
internal sealed class NativeCallbackBinding(OpenIddictClientEvents.ProcessAuthenticationContext context)
{
    private readonly ClaimsPrincipal principal = context.StateTokenPrincipal ?? throw ProtocolStorageOwner.Unavailable();
    private readonly OpenIddictRequest request = context.Request;
    private readonly OpenIddictClientOptions options = context.Options;
    private readonly string claims = NativeStateBinding.Claims(context.StateTokenPrincipal!);
    private readonly string input = Input(context.Request);

    /// <summary>Requires the original validated objects and complete values before/after native reads, writes and actual child cleanup.</summary>
    internal void RequireCurrent()
    {
        if (!ReferenceEquals(principal, context.StateTokenPrincipal) || !ReferenceEquals(request, context.Request) ||
            !ReferenceEquals(options, context.Options) || claims != NativeStateBinding.Claims(principal) || input != Input(request))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
    }

    /// <summary>Retains only a digest of callback parameters; no code or state value enters diagnostics.</summary>
    private static string Input(OpenIddictRequest value) => FactorBinding.Digest(value.GetParameters().OrderBy(pair => pair.Key, StringComparer.Ordinal)
        .Select(pair => new { pair.Key, Value = pair.Value.GetRawValue() }).ToArray());
}
