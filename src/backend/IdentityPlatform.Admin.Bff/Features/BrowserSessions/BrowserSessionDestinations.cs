using IdentityPlatform.Core.Features.Configuration;

namespace IdentityPlatform.Admin.Bff.Features.BrowserSessions;

/// <summary>Fixes browser destinations from explicit canonical HTTPS identities, never incoming Host, forwarded or return parameters.</summary>
internal sealed class BrowserSessionDestinations
{
    internal const string IssuerEntryPath = "/account/administrator/browser/login";
    internal const string ResumePath = "/bff/login/resume";
    internal const string LogoutReturnPath = "/bff/logout/return";
    private const string Invalid = "Browser session destinations are unavailable.";
    internal Uri Issuer
    {
        get;
    }
    internal Uri Client
    {
        get;
    }
    internal string Origin => Client.GetLeftPart(UriPartial.Authority);
    internal Uri Entry => new(Issuer, IssuerEntryPath);
    internal Uri Resume => new(Client, ResumePath);
    internal Uri LogoutReturn => new(Client, LogoutReturnPath);

    /// <summary>Retains only validated immutable identities; constructing policy does not enable routes or authentication schemes.</summary>
    internal BrowserSessionDestinations(string issuer, string client)
    {
        Issuer = CanonicalUri(issuer);
        Client = CanonicalUri(client);
    }

    /// <summary>Rejects alternate spellings and non-HTTPS input without including supplied values in diagnostics.</summary>
    private static Uri CanonicalUri(string value)
    {
        var result = IssuerUri.Parse(value);
        return result.AbsoluteUri == value ? result : throw new ArgumentException(Invalid);
    }
}
