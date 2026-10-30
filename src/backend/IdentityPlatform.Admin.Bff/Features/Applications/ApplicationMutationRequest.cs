using System.Text.Json;
using IdentityPlatform.Admin.Bff.Features.BrowserSessions;
using IdentityPlatform.Core.Features.Applications;
using Microsoft.AspNetCore.Http.Features;

namespace IdentityPlatform.Admin.Bff.Features.Applications;

/// <summary>Freezes one validated finite JSON operation; browser headers never become arbitrary downstream headers or destinations.</summary>
internal sealed class ApplicationMutationRequest
{
    private const string CurrentSegment = ".";
    private const string ParentSegment = "..";
    private const string TransferEncoding = "Transfer-Encoding";
    private const string ContentEncoding = "Content-Encoding";
    private const string ExpiryFormat = "yyyy-MM-dd'T'HH:mm:ss'Z'";
    private const string PathSeparator = "/";

    internal const string ApiPath = "/api/v1/applications";
    internal const string ReplacementSuffix = "/secret-replacements";
    internal const string JsonType = "application/json";
    private const string JsonUtf8 = "application/json; charset=utf-8";
    private const int RevisionOffset = 66;
    private const int VersionPunctuation = 67;
    private const int BodyBytes = 32768;
    private const int TokenCharacters = 4096;
    internal ApplicationProxyAction Action
    {
        get;
    }
    internal string? Id
    {
        get;
    }
    internal string? Version
    {
        get;
    }
    internal ReadOnlyMemory<byte> Body
    {
        get;
    }
    private readonly object input;
    internal string Path => ApiPath + ResourcePath(Action, Id);
    internal HttpMethod Method => Action == ApplicationProxyAction.Edit ? HttpMethod.Put : HttpMethod.Post;

    /// <summary>Keeps validated original bytes private to a newly parsed command; no request body or mutable DTO survives by reference.</summary>
    private ApplicationMutationRequest(ApplicationProxyAction action, string? id, string? version, byte[] body, object input)
    {
        Action = action;
        Id = id;
        Version = version;
        Body = body;
        this.input = input;
    }

    /// <summary>Checks framing and preconditions before reading finite actual bytes and validating the selected Core contract.</summary>
    internal static async Task<ApplicationMutationRequest> ReadAsync(HttpContext http, ApplicationProxyAction action, string? id, DateTimeOffset now)
    {
        if (!Enum.IsDefined(action) || (action == ApplicationProxyAction.Create ? id is not null : !ApplicationEditorRules.IsText(id, ApplicationProjection.IdentifierLength) || id is CurrentSegment or ParentSegment))
        {
            throw new ApplicationProxyRejected(StatusCodes.Status400BadRequest);
        }
        var path = ApplicationProxyEndpoint.Path + ResourcePath(action, id);
        var request = http.Request;
        var method = action == ApplicationProxyAction.Edit ? HttpMethods.Put : HttpMethods.Post;
        if (!ApplicationBrowserRequest.Origin(http, required: true) || !ApplicationBrowserRequest.Exact(http, path) || request.Method != method ||
            request.ContentType is not (JsonType or JsonUtf8) || request.ContentLength is < 0 or > BodyBytes ||
            request.Headers.ContainsKey(TransferEncoding) || request.Headers.ContainsKey(ContentEncoding) ||
            request.Headers[BrowserRequestPolicy.JsonAntiforgeryHeader] is not { Count: 1 } csrf || csrf[0] is not { Length: > 0 and <= TokenCharacters } ||
            http.Features.Get<IFormFeature>() is not null)
        {
            throw new ApplicationProxyRejected(StatusCodes.Status400BadRequest);
        }
        var version = ReadVersion(request, action);
        try
        {
            var bytes = await ApplicationJson.ReadAsync(request.Body, BodyBytes, http.RequestAborted);
            if (request.ContentLength is { } declared && declared != bytes.Length)
            {
                throw new InvalidDataException();
            }
            return new(action, id, version, bytes, Validate(bytes, action, now));
        }
        catch (Exception error) when (error is JsonException or InvalidDataException or ArgumentException)
        {
            throw new ApplicationProxyRejected(StatusCodes.Status400BadRequest);
        }
    }

    /// <summary>Allows only a single canonical strong precondition and distinguishes missing from malformed input.</summary>
    private static string? ReadVersion(HttpRequest request, ApplicationProxyAction action)
    {
        var values = request.Headers.IfMatch;
        if (action == ApplicationProxyAction.Create)
        {
            if (values.Count != 0)
            {
                throw new ApplicationProxyRejected(StatusCodes.Status400BadRequest);
            }
            return null;
        }
        if (values.Count == 0)
        {
            throw new ApplicationProxyRejected(StatusCodes.Status428PreconditionRequired);
        }
        if (values.Count != 1 || !ApplicationVersion.IsCanonical(values[0]))
        {
            throw new ApplicationProxyRejected(StatusCodes.Status412PreconditionFailed);
        }
        return values[0];
    }

    /// <summary>Parses only the selected independently validated DTO; unknown, duplicate and null fields cannot reach the API.</summary>
    private static object Validate(byte[] bytes, ApplicationProxyAction action, DateTimeOffset now)
    {
        object value;
        IReadOnlyDictionary<string, string[]> errors;
        if (action == ApplicationProxyAction.Create)
        {
            var input = ApplicationJson.Parse<ApplicationCreate>(bytes);
            value = input;
            errors = ApplicationEditorRules.Validate(input, now);
        }
        else if (action == ApplicationProxyAction.Edit)
        {
            var input = ApplicationJson.Parse<ApplicationEdit>(bytes);
            value = input;
            errors = ApplicationEditorRules.Validate(input);
        }
        else
        {
            var input = ApplicationJson.Parse<ApplicationSecretReplacement>(bytes);
            value = input;
            errors = ApplicationEditorRules.Validate(input, now);
        }
        if (errors.Count != 0)
        {
            throw new InvalidDataException();
        }
        return value;
    }

    /// <summary>Binds returned fields and exact successor revision to this immutable parsed action, without trusting response shape alone.</summary>
    internal bool Matches(ApplicationDetails details, long revision)
    {
        if (Action != ApplicationProxyAction.Create)
        {
            var previous = long.Parse(Version!.AsSpan(RevisionOffset, Version!.Length - VersionPunctuation), System.Globalization.CultureInfo.InvariantCulture);
            if (Id != details.Id || previous == long.MaxValue || revision != previous + 1 || !ApplicationVersion.Matches(Version, Id!, previous))
            {
                return false;
            }
        }
        return input switch
        {
            ApplicationCreate create => revision == 1 && details.ClientId == create.ClientId && details.DisplayName == create.DisplayName &&
                SameRedirects(details, create.RedirectUris) && SameExpiry(details, create.CredentialExpiresAt),
            ApplicationEdit edit => details.DisplayName == edit.DisplayName && SameRedirects(details, edit.RedirectUris),
            ApplicationSecretReplacement replacement => SameExpiry(details, replacement.CredentialExpiresAt),
            _ => false
        };
    }

    /// <summary>Compares the exact callback set independently of native manager enumeration order.</summary>
    private static bool SameRedirects(ApplicationDetails details, IReadOnlyList<string> expected) =>
        details.RedirectUris.Order(StringComparer.Ordinal).SequenceEqual(expected.Order(StringComparer.Ordinal));

    /// <summary>Retains the operator's exact credential deadline instead of accepting a silently changed lifetime.</summary>
    private static bool SameExpiry(ApplicationDetails details, string expected) => details.Credential is { } credential &&
        credential.ExpiresAt.ToString(ExpiryFormat, System.Globalization.CultureInfo.InvariantCulture) == expected;

    /// <summary>Encodes one opaque ID as a path segment; operation suffixes are code-owned.</summary>
    private static string ResourcePath(ApplicationProxyAction action, string? id) => action == ApplicationProxyAction.Create ? string.Empty :
        PathSeparator + Uri.EscapeDataString(id!) + (action == ApplicationProxyAction.Replace ? ReplacementSuffix : string.Empty);
}

/// <summary>Closes the writable transport surface to the three reviewed application actions.</summary>
internal enum ApplicationProxyAction
{
    Create, Edit, Replace
}

/// <summary>Contains only a finite public status, never submitted values or downstream exception details.</summary>
internal sealed class ApplicationProxyRejected(int status) : Exception
{
    internal int Status => status;
}
