using OpenIddict.Abstractions;

namespace IdentityPlatform.Testing;

/// <summary>Reports only fixed native error categories and exact known documentation identifiers, never response values.</summary>
internal static class NativeClientRejection
{
    internal const string None = "none";
    internal const string Property = "test-native-client-rejection";
    private const string Documentation = "https://documentation.openiddict.com/errors/";
    private const string Separator = "/";
    private const string Unknown = "unknown";
    private static readonly string[] Identifiers =
    [
        "ID2000", "ID2004", "ID2019", "ID2029", "ID2088", "ID2089", "ID2090", "ID2091", "ID2093", "ID2094", "ID2119", "ID2120", "ID2121", "ID2122",
        "ID2123", "ID2124", "ID2126", "ID2129", "ID2130", "ID2138", "ID2139", "ID2142", "ID2163", "ID2164",
        "ID2170", "ID2184", "ID2185"
    ];

    /// <summary>Maps exact allowlisted values; arbitrary errors, URI suffixes, descriptions, tokens and cookies cannot enter the result.</summary>
    internal static string Describe(string? error, string? uri)
    {
        var category = error switch
        {
            OpenIddictConstants.Errors.InvalidRequest => nameof(OpenIddictConstants.Errors.InvalidRequest),
            OpenIddictConstants.Errors.InvalidToken => nameof(OpenIddictConstants.Errors.InvalidToken),
            OpenIddictConstants.Errors.MissingToken => nameof(OpenIddictConstants.Errors.MissingToken),
            OpenIddictConstants.Errors.ServerError => nameof(OpenIddictConstants.Errors.ServerError),
            OpenIddictConstants.Errors.InvalidGrant => nameof(OpenIddictConstants.Errors.InvalidGrant),
            OpenIddictConstants.Errors.LoginRequired => nameof(OpenIddictConstants.Errors.LoginRequired),
            _ => Unknown
        };
        return category + Separator + (Array.Find(Identifiers, value => string.Equals(uri, Documentation + value, StringComparison.Ordinal)) ?? Unknown);
    }
}
