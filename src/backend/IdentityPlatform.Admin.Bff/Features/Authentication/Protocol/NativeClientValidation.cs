using System.Globalization;
using System.Security.Claims;
using IdentityPlatform.Admin.Bff.Features.Authentication.Ownership;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using OpenIddict.Client;

namespace IdentityPlatform.Admin.Bff.Features.Authentication.Protocol;

/// <summary>Constrains the actual native validation parameters after its compatibility defaults have been applied.</summary>
internal sealed class NativeClientValidationParameters(NativeClientProfile profile) : IOpenIddictClientHandler<OpenIddictClientEvents.ValidateTokenContext>
{
    /// <summary>Retains native state handling and pins server identity tokens to the fixed issuer, algorithm and native identity-token type.</summary>
    public ValueTask HandleAsync(OpenIddictClientEvents.ValidateTokenContext context)
    {
        if (context.ValidTokenTypes.SetEquals([OpenIddictConstants.TokenTypeIdentifiers.IdentityToken]))
        {
            context.TokenValidationParameters.ValidIssuers = [profile.Issuer.AbsoluteUri];
            context.TokenValidationParameters.ValidAlgorithms = [SecurityAlgorithms.RsaSha256];
            context.TokenValidationParameters.ValidTypes = [OpenIddictConstants.JsonWebTokenTypes.GenericJsonWebToken];
        }
        return ValueTask.CompletedTask;
    }
}

/// <summary>Adds strict identity-token lifetime limits after native cryptographic and lifetime processing.</summary>
internal sealed class NativeClientTokenTime : IOpenIddictClientHandler<OpenIddictClientEvents.ValidateTokenContext>
{
    /// <summary>Does not reject state early: durable redemption still consumes validated expired/error callbacks before final rejection.</summary>
    public ValueTask HandleAsync(OpenIddictClientEvents.ValidateTokenContext context)
    {
        if (context.ValidTokenTypes.SetEquals([OpenIddictConstants.TokenTypeIdentifiers.IdentityToken]) &&
            !Current(context.Principal, context.Options.TimeProvider!))
        {
            context.Reject(OpenIddictConstants.Errors.InvalidToken);
        }
        return ValueTask.CompletedTask;
    }

    /// <summary>Requires explicit finite dates and strictly positive remaining lifetime without extending expiry by native skew.</summary>
    internal static bool Current(System.Security.Claims.ClaimsPrincipal? principal, TimeProvider clock)
    {
        var created = NumericDate(principal, OpenIddictConstants.Claims.IssuedAt);
        var expires = NumericDate(principal, OpenIddictConstants.Claims.ExpiresAt);
        var now = clock.GetUtcNow();
        return created is { } issued && expires is { } end && end > now && end > issued &&
            issued <= now.AddSeconds(AdministratorAssertion.ClockSkewSeconds) &&
            end - issued <= TimeSpan.FromSeconds(AdministratorRegistrationPolicy.TokenLifetimeSeconds);
    }

    /// <summary>Reads one public integral NumericDate from native ID-token output; duplicate, textual or unrepresentable dates fail closed.</summary>
    private static DateTimeOffset? NumericDate(ClaimsPrincipal? principal, string name)
    {
        const int DuplicateCount = 2;
        var claims = principal?.FindAll(name).Take(DuplicateCount).ToArray();
        if (claims is not [{ } claim] || claim.ValueType is not (ClaimValueTypes.Integer or ClaimValueTypes.Integer32 or
            ClaimValueTypes.Integer64 or ClaimValueTypes.UInteger32 or ClaimValueTypes.UInteger64) ||
            !long.TryParse(claim.Value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var seconds) ||
            seconds < DateTimeOffset.MinValue.ToUnixTimeSeconds() || seconds > DateTimeOffset.MaxValue.ToUnixTimeSeconds())
        {
            return null;
        }
        return DateTimeOffset.FromUnixTimeSeconds(seconds);
    }
}

/// <summary>Rechecks the whole callback lifetime after remote transport and native ID-token/nonce/correlation processing.</summary>
internal sealed class NativeClientAuthenticationCompletion : IOpenIddictClientHandler<OpenIddictClientEvents.ProcessAuthenticationContext>
{
    /// <summary>Rejects missing or expired state/identity output before native merged-principal publication.</summary>
    public ValueTask HandleAsync(OpenIddictClientEvents.ProcessAuthenticationContext context)
    {
        if (context.StateTokenPrincipal is null || !DurableClientState.IsCurrent(context.StateTokenPrincipal, context.Options.TimeProvider!) ||
            !NativeClientTokenTime.Current(context.BackchannelIdentityTokenPrincipal, context.Options.TimeProvider!))
        {
            context.Reject(OpenIddictConstants.Errors.InvalidToken);
            return ValueTask.CompletedTask;
        }
        NativeClientCallback.Find(context).Require(context);
        return ValueTask.CompletedTask;
    }
}
