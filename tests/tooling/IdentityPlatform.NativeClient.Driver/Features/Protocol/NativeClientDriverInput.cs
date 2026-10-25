using IdentityPlatform.NativeClient.Driver.Features.BrowserSessions;

namespace IdentityPlatform.NativeClient.Driver.Features.Protocol;

/// <summary>Limits the test-only process to a local TLS issuer, one fixed native client profile and separately protected synthetic credentials.</summary>
internal static class NativeClientDriverInput
{
    private const int TextLimit = 8_192;
    private const string Client = "https://administrator.example.test/";
    private const string Callback = "https://administrator.example.test/callback";
    private static readonly string[] CredentialNames =
    [
        "ClientCredentials:StateSigning:Path", "ClientCredentials:StateSigning:Password",
        "ClientCredentials:StateEncryption:Path", "ClientCredentials:StateEncryption:Password",
        "ClientCredentials:AssertionSigning:Path", "ClientCredentials:AssertionSigning:Password"
    ];

    /// <summary>Accepts only known profile/credential fields and an exact callback destination, never arbitrary operations or SQL.</summary>
    internal static void Require(NativeClientProcessInput input)
    {
        _ = DatabaseProviderParser.Parse(input.Provider);
        Text(input.RuntimeConnection);
        Text(input.KeyId);
        Text(input.Issuer);
        var issuer = new Uri(input.Issuer, UriKind.Absolute);
        if (issuer.Scheme != Uri.UriSchemeHttps || (input.Browser is null ? !issuer.IsLoopback : input.Issuer != BrowserDriverInput.Issuer) || issuer.UserInfo.Length != 0 || issuer.Query.Length != 0 ||
            issuer.Fragment.Length != 0 || input.Client != Client || input.Callback != Callback ||
            input.Credentials is null || !input.Credentials.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(CredentialNames))
        {
            throw new InvalidOperationException();
        }
        foreach (var value in input.Credentials.Values)
        {
            Text(value);
        }
        _ = DateTimeOffset.FromUnixTimeMilliseconds(input.UtcMilliseconds);
        if (input.Browser is { } browser)
        {
            BrowserDriverInput.Require(input, browser);
            return;
        }
        if (input.RequestUri is null)
        {
            if (input.Cookie is not null)
            {
                throw new InvalidOperationException();
            }
            return;
        }
        Text(input.RequestUri);
        Text(input.Cookie);
        var callback = new Uri(input.RequestUri, UriKind.Absolute);
        if (callback.GetLeftPart(UriPartial.Path) != Callback || callback.Fragment.Length != 0 || callback.UserInfo.Length != 0)
        {
            throw new InvalidOperationException();
        }
    }

    /// <summary>Rejects oversized, missing or control-bearing input without including its value in an exception.</summary>
    internal static void Text(string? value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > TextLimit || value.Any(char.IsControl))
        {
            throw new InvalidOperationException();
        }
    }
}
