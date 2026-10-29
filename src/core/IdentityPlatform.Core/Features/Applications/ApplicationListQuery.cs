using System.Globalization;

namespace IdentityPlatform.Core.Features.Applications;

/// <summary>Validates the finite application collection query shared by browser and management projections.</summary>
/// <example><code>var query = ApplicationListQuery.Parse(new Dictionary&lt;string, string[]&gt; { ["pageSize"] = ["25"] });</code></example>
public sealed class ApplicationListQuery
{
    public const string SearchParameter = "search";
    public const string PageParameter = "page";
    public const string SizeParameter = "pageSize";
    public const int DefaultSize = 25;
    public const int MaximumSize = 100;
    public const int MaximumPage = 10_000;
    public const int MaximumSearch = 100;
    private const int MaximumParameters = 3;
    public string Search
    {
        get;
    }
    public int Page
    {
        get;
    }
    public int PageSize
    {
        get;
    }

    /// <summary>Retains already validated query values without exposing mutable transport collections.</summary>
    private ApplicationListQuery(string search, int page, int pageSize)
    {
        Search = search;
        Page = page;
        PageSize = pageSize;
    }

    /// <summary>Rejects duplicate, unknown, noncanonical and excessive parameters without normalizing protocol identifiers.</summary>
    /// <example><code>var query = ApplicationListQuery.Parse(new Dictionary&lt;string, string[]&gt;()); // First page, 25 records.</code></example>
    public static ApplicationListQuery? Parse(IEnumerable<KeyValuePair<string, string[]>> values)
    {
        var fields = values.Take(MaximumParameters + 1).ToArray();
        if (fields.Length > MaximumParameters || fields.Select(field => field.Key).Distinct(StringComparer.Ordinal).Count() != fields.Length ||
            fields.Any(field => field.Key is not (SearchParameter or PageParameter or SizeParameter) || field.Value.Length != 1))
        {
            return null;
        }
        var query = fields.ToDictionary(field => field.Key, field => field.Value[0], StringComparer.Ordinal);
        var search = query.GetValueOrDefault(SearchParameter) ?? string.Empty;
        if (search.Length > MaximumSearch || search.Any(char.IsControl) ||
            !ReadNumber(query, PageParameter, 1, MaximumPage, out var page) ||
            !ReadNumber(query, SizeParameter, DefaultSize, MaximumSize, out var size))
        {
            return null;
        }
        return new(search, page, size);
    }

    /// <summary>Requires one canonical unsigned decimal within the explicit positive bound.</summary>
    private static bool ReadNumber(Dictionary<string, string> query, string key, int fallback, int maximum, out int value)
    {
        value = fallback;
        return !query.TryGetValue(key, out var raw) || int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out value) &&
            value is >= 1 && value <= maximum && value.ToString(CultureInfo.InvariantCulture) == raw;
    }
}
