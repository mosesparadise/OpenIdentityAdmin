using System.Globalization;
using IdentityPlatform.AuthServer.Features.Protocol.Administrator;
using System.Security.Cryptography;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.Identity.Journeys;
using Microsoft.AspNetCore.DataProtection;
using OpenIddict.Abstractions;

namespace IdentityPlatform.AuthServer.Features.Accounts.Browser;

/// <summary>Protects an immutable confirmation of one native logout request and the actual authenticated source session.</summary>
internal sealed class IssuerLogoutConfirmation(IDataProtectionProvider protection, TimeProvider clock)
{
    private const string Purpose = "IdentityPlatform.AuthServer.IssuerLogoutConfirmation.v1";
    private const int LifetimeSeconds = 300;
    private const int Fields = 3;
    private const int ExpirationIndex = 2;
    private const int MaximumLength = 4096;
    private readonly IDataProtector protector = protection.CreateProtector(Purpose);

    /// <summary>Creates no authority and preserves the shorter original source deadline.</summary>
    internal string Create(OpenIddictRequest request, JourneySession session)
    {
        var now = clock.GetUtcNow().ToUnixTimeSeconds();
        var expires = Math.Min(checked(now + LifetimeSeconds), session.ExpiresAt);
        return protector.Protect(string.Join(':', Binding(request, session.UserId, session.SessionId),
            now.ToString(CultureInfo.InvariantCulture), expires.ToString(CultureInfo.InvariantCulture)));
    }

    /// <summary>Requires unchanged native fields and subject/session, strict original expiry and no clock rollback.</summary>
    internal JourneyLogoutDeadline? Read(string? value, OpenIddictRequest request, JourneySessionCandidate candidate)
    {
        if (value is not { Length: > 0 and <= MaximumLength })
        {
            return null;
        }
        try
        {
            var fields = protector.Unprotect(value).Split(':');
            var now = clock.GetUtcNow().ToUnixTimeSeconds();
            return fields.Length == Fields && fields[0] == Binding(request, candidate.UserId, candidate.SessionId) &&
                long.TryParse(fields[1], NumberStyles.None, CultureInfo.InvariantCulture, out var created) &&
                long.TryParse(fields[ExpirationIndex], NumberStyles.None, CultureInfo.InvariantCulture, out var expires) &&
                created > 0 && expires > created && expires - created <= LifetimeSeconds && now >= created && now < expires ? new JourneyLogoutDeadline(created, expires) : null;
        }
        catch (Exception error) when (error is CryptographicException or FormatException) { return null; }
    }

    /// <summary>Separates source identity and each public native request value without ambiguity or token-bearing input.</summary>
    private static string Binding(OpenIddictRequest request, string user, string session) => FactorBinding.Digest(new
    {
        User = user,
        Session = session,
        request.ClientId,
        request.PostLogoutRedirectUri,
        Hint = IssuerLogoutInput.Hint(request),
        request.State
    });
}
