using IdentityPlatform.Core.Features.Configuration;

namespace IdentityPlatform.AuthServer.Features.Accounts.Browser;

/// <summary>Fixes the issuer origin and single BFF return from explicit configuration, with no submitted client or return destination.</summary>
internal sealed class BrowserAccountProfile
{
    internal const string LoginPath = "/account/administrator/browser/login";
    internal const string MfaPath = "/account/administrator/browser/mfa";
    internal const string Intent = "Intent";
    internal const string Completion = "Completion";
    private const string ResumePath = "/bff/login/resume";
    private const string Unavailable = "Browser account destinations are unavailable.";
    internal string Origin
    {
        get;
    }
    internal Uri Resume
    {
        get;
    }

    /// <summary>Preserves canonical native identity URI semantics while deriving only the fixed root-relative return.</summary>
    internal BrowserAccountProfile(string issuer, string client)
    {
        var server = IssuerUri.Parse(issuer);
        var destination = IssuerUri.Parse(client);
        if (server.AbsoluteUri != issuer || destination.AbsoluteUri != client)
        {
            throw new ArgumentException(Unavailable);
        }
        Origin = server.GetLeftPart(UriPartial.Authority);
        Resume = new(destination, ResumePath);
    }
}
