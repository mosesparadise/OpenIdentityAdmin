using System.Globalization;
using OpenIddict.Abstractions;

namespace IdentityPlatform.AuthServer.Features.Protocol.Policy;

public static class AuthenticationFreshness
{
    public static bool TryGetMaximumAge(OpenIddictRequest request, out long? maximumAge)
    {
        maximumAge = null;
        if (!request.HasParameter(OpenIddictConstants.Parameters.MaxAge))
        {
            return true;
        }
        var raw = (string?)request.GetParameter(OpenIddictConstants.Parameters.MaxAge);
        if (string.IsNullOrEmpty(raw) || !raw.All(char.IsAsciiDigit) ||
            !long.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var age))
        {
            return false;
        }
        maximumAge = age;
        return true;
    }

    public static bool Required(OpenIddictRequest request, long? maximumAge, long authenticatedAt, long now) =>
        request.HasPromptValue(OpenIddictConstants.PromptValues.Login) ||
        maximumAge is 0 || maximumAge is { } age && now - authenticatedAt > age;
}
