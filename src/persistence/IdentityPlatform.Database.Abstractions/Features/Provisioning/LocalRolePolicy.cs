namespace IdentityPlatform.Database;

/// <summary>Owns fixed local runtime principal names and the safe password alphabet used by privileged DDL.</summary>
/// <example><code>LocalRolePolicy.ValidatePassword(generatedPassword);</code></example>
public static class LocalRolePolicy
{
    private const int MinimumLength = 32;
    private const int MaximumLength = 128;
    private const string InvalidPassword = "Invalid local runtime password format.";
    private const string PlatformRole = "platform_runtime";
    private const string SessionsRole = "sessions_runtime";

    /// <summary>Requires bounded ASCII alphanumeric passwords containing uppercase, lowercase and digits.</summary>
    /// <example><code>LocalRolePolicy.ValidatePassword(generatedPassword); // Never log this value.</code></example>
    public static void ValidatePassword(string password)
    {
        ArgumentNullException.ThrowIfNull(password);
        if (password.Length is < MinimumLength or > MaximumLength || !password.All(char.IsAsciiLetterOrDigit) ||
            !password.Any(char.IsAsciiLetterUpper) || !password.Any(char.IsAsciiLetterLower) || !password.Any(char.IsAsciiDigit))
        {
            throw new ArgumentException(InvalidPassword, nameof(password));
        }
    }

    /// <summary>Maps a logical store to its fixed least-privilege Development principal.</summary>
    /// <example><code>var role = LocalRolePolicy.Role(DatabaseStore.Platform);</code></example>
    public static string Role(DatabaseStore store) => store switch
    {
        DatabaseStore.Platform => PlatformRole,
        DatabaseStore.Sessions => SessionsRole,
        _ => throw new ArgumentOutOfRangeException(nameof(store))
    };
}
