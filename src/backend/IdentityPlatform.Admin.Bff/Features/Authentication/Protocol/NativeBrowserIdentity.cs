using IdentityPlatform.Admin.Bff.Features.BrowserSessions;
using OpenIddict.Abstractions;
using OpenIddict.Client;

namespace IdentityPlatform.Admin.Bff.Features.Authentication.Protocol;

/// <summary>Checks B3 signed claims only after complete native callback validation and consumed-state receipt validation.</summary>
internal sealed class NativeBrowserIdentity : IOpenIddictClientHandler<OpenIddictClientEvents.ProcessAuthenticationContext>
{
    /// <summary>Requires exact protected expectation and signed B3 data together; it never establishes a ticket or emits a cookie.</summary>
    public ValueTask HandleAsync(OpenIddictClientEvents.ProcessAuthenticationContext context)
    {
        if (!Matches(context))
        {
            context.Reject(OpenIddictConstants.Errors.InvalidToken);
        }
        return ValueTask.CompletedTask;
    }

    /// <summary>Rejects malformed presence and unrequested B3 claims instead of silently downgrading them to a B2 identity.</summary>
    internal static bool Matches(OpenIddictClientEvents.ProcessAuthenticationContext context)
    {
        if (context.StateTokenPrincipal is not { } state || context.BackchannelIdentityTokenPrincipal is not { } identity)
        {
            return false;
        }
        var present = state.Claims.Any(claim => string.Equals(claim.Type, BrowserSessionIntent.ClaimType, StringComparison.OrdinalIgnoreCase));
        if (!present)
        {
            return !identity.Claims.Any(claim => string.Equals(claim.Type, BrowserSessionClaims.Intent, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(claim.Type, BrowserSessionClaims.SourceExpiry, StringComparison.OrdinalIgnoreCase));
        }
        return BrowserSessionIntent.Read(state) is { } expected && context.BackchannelIdentityToken is { Length: > 0 } encoded &&
            BrowserSessionIdentity.Validate(identity, encoded, expected, context.Options.TimeProvider!.GetUtcNow()) is not null;
    }
}
