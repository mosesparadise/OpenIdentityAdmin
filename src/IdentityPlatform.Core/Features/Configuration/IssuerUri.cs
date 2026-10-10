namespace IdentityPlatform.Core.Features.Configuration;

public static class IssuerUri
{
    public static Uri Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Any(char.IsWhiteSpace) ||
            !Uri.TryCreate(value, UriKind.Absolute, out var issuer) ||
            issuer.Scheme != Uri.UriSchemeHttps || string.IsNullOrEmpty(issuer.Host) ||
            !string.IsNullOrEmpty(issuer.UserInfo) || !string.IsNullOrEmpty(issuer.Query) ||
            !string.IsNullOrEmpty(issuer.Fragment))
        {
            // Never include the supplied URI: userinfo/query strings may contain secrets.
            throw new ArgumentException(
                "Identity:Issuer must be an absolute HTTPS URI without userinfo, query or fragment.");
        }

        return issuer;
    }
}
