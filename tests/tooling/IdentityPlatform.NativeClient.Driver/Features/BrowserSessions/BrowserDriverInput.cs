using IdentityPlatform.NativeClient.Driver.Features.Protocol;

namespace IdentityPlatform.NativeClient.Driver.Features.BrowserSessions;

/// <summary>Defines a closed local-only test surface with explicit retained-key configuration and no arbitrary URI or body inputs.</summary>
internal static class BrowserDriverInput
{
    internal const string Issuer = "https://issuer.example.test/";
    private static readonly string[] ProtectionNames =
    [
        "Bff:Sessions:Protection:ApplicationId", "Bff:Sessions:Protection:KeyRingPath",
        "Bff:Sessions:Protection:Current:Path", "Bff:Sessions:Protection:Current:Password"
    ];

    /// <summary>Rejects mixed native fields, unknown operations, unknown ring fields and irrelevant tokens before loading any credentials.</summary>
    internal static void Require(NativeClientProcessInput input, BrowserProcessRequest browser)
    {
        if (input.RequestUri is not null || !Enum.IsDefined(browser.Operation) || browser.Protection is null ||
            !browser.Protection.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(ProtectionNames) ||
            browser.Operation != BrowserProcessOperation.LogoutSubmit && browser.FormToken is not null)
        {
            throw new InvalidOperationException();
        }
        if (browser.UtcTicks <= 0 || browser.UtcTicks > DateTimeOffset.MaxValue.UtcTicks ||
            new DateTimeOffset(browser.UtcTicks, TimeSpan.Zero).ToUnixTimeMilliseconds() != input.UtcMilliseconds)
        {
            throw new InvalidOperationException();
        }
        NativeClientDriverInput.Text(input.Cookie);
        foreach (var value in browser.Protection.Values)
        {
            NativeClientDriverInput.Text(value);
        }
        if (browser.Operation == BrowserProcessOperation.LogoutSubmit)
        {
            NativeClientDriverInput.Text(browser.FormToken);
        }
    }
}
