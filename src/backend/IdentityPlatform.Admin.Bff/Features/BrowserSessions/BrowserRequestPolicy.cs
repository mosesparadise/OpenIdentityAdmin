using Microsoft.Extensions.Primitives;

namespace IdentityPlatform.Admin.Bff.Features.BrowserSessions;

/// <summary>Supplies pure request/form predicates; route adapters must bound actual bytes before native parsing and antiforgery validation.</summary>
internal static class BrowserRequestPolicy
{
    internal const string AntiforgeryField = "__RequestVerificationToken";
    internal const int BodyBytes = 8192;
    internal const int ValueLength = 4096;
    private const string FormMedia = "application/x-www-form-urlencoded";
    private const string FormUtf8 = "application/x-www-form-urlencoded; charset=utf-8";
    private const string OriginHeader = "Origin";
    private const string AuthorizationHeader = "Authorization";

    /// <summary>Requires exact configured origin and reviewed POST media; protocol GET exceptions must use separate native guards.</summary>
    internal static bool Mutation(HttpRequest request, BrowserSessionDestinations destinations) =>
        HttpMethods.IsPost(request.Method) && request.IsHttps && request.Query.Count == 0 &&
        !request.Headers.ContainsKey(AuthorizationHeader) &&
        request.Headers[OriginHeader] is { Count: 1 } origin && origin[0] == destinations.Origin &&
        request.ContentType is FormMedia or FormUtf8 && request.ContentLength is null or >= 0 and <= BodyBytes;

    /// <summary>Rejects decoded aliases, duplicates, files, unexpected fields and oversized values independently of parser configuration.</summary>
    internal static bool Form(IFormCollection form, params string[] fields)
    {
        var allowed = fields.Append(AntiforgeryField).ToHashSet(StringComparer.Ordinal);
        return allowed.Count == fields.Length + 1 && form.Files.Count == 0 && form.Count == allowed.Count &&
            form.Keys.All(allowed.Contains) && allowed.All(field => Single(form[field]));
    }

    /// <summary>Requires one nonempty bounded value rather than accepting native concatenation of duplicate inputs.</summary>
    private static bool Single(StringValues value) => value is { Count: 1 } && value[0] is { Length: > 0 and <= ValueLength };
}
