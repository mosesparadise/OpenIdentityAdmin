using System.Text.Json;
using IdentityPlatform.Admin.Bff.Features.BrowserSessions;
using IdentityPlatform.Core.Features.ApplicationGrants;
using IdentityPlatform.Core.Features.Applications;
using IdentityPlatform.Core.Features.Catalogue;
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
    internal string? RequestKey
    {
        get;
    }
    private readonly object input;
    internal string Path => ApiPath + ResourcePath(Action, Id);
    internal HttpMethod Method => Action is ApplicationProxyAction.Edit or ApplicationProxyAction.AssignScopes ? HttpMethod.Put : HttpMethod.Post;

    /// <summary>Keeps validated original bytes private to a newly parsed command; no request body or mutable DTO survives by reference.</summary>
    private ApplicationMutationRequest(ApplicationProxyAction action, string? id, string? version, byte[] body, object input, string? requestKey)
    {
        Action = action;
        Id = id;
        Version = version;
        Body = body;
        this.input = input;
        RequestKey = requestKey;
    }

    /// <summary>Checks framing and preconditions before reading finite actual bytes and validating the selected Core contract.</summary>
    internal static async Task<ApplicationMutationRequest> ReadAsync(HttpContext http, ApplicationProxyAction action, string? id, DateTimeOffset now)
    {
        if (!Enum.IsDefined(action) || (action is ApplicationProxyAction.Create or ApplicationProxyAction.CreateNative or ApplicationProxyAction.CreateBrowser or ApplicationProxyAction.CreateMachine ? id is not null : !ApplicationEditorRules.IsText(id, ApplicationProjection.IdentifierLength) || id is CurrentSegment or ParentSegment))
        {
            throw new ApplicationProxyRejected(StatusCodes.Status400BadRequest);
        }
        var path = ApplicationProxyEndpoint.Path + ResourcePath(action, id);
        var request = http.Request;
        var method = action is ApplicationProxyAction.Edit or ApplicationProxyAction.AssignScopes ? HttpMethods.Put : HttpMethods.Post;
        if (!ApplicationBrowserRequest.Origin(http, required: true) || !ApplicationBrowserRequest.Exact(http, path) || request.Method != method ||
            request.ContentType is not (JsonType or JsonUtf8) || request.ContentLength is < 0 or > BodyBytes ||
            request.Headers.ContainsKey(TransferEncoding) || request.Headers.ContainsKey(ContentEncoding) ||
            request.Headers[BrowserRequestPolicy.JsonAntiforgeryHeader] is not { Count: 1 } csrf || csrf[0] is not { Length: > 0 and <= TokenCharacters } ||
            http.Features.Get<IFormFeature>() is not null)
        {
            throw new ApplicationProxyRejected(StatusCodes.Status400BadRequest);
        }
        var version = ReadVersion(request, action);
        var key = ReadKey(request, action);
        try
        {
            var bytes = await ApplicationJson.ReadAsync(request.Body, BodyBytes, http.RequestAborted);
            if (request.ContentLength is { } declared && declared != bytes.Length)
            {
                throw new InvalidDataException();
            }
            return new(action, id, version, bytes, Validate(bytes, action, now), key);
        }
        catch (Exception error) when (error is JsonException or InvalidDataException or ArgumentException)
        {
            throw new ApplicationProxyRejected(StatusCodes.Status400BadRequest);
        }
    }

    /// <summary>Requires a canonical key only for the separately replayable terminal operation.</summary>
    private static string? ReadKey(HttpRequest request, ApplicationProxyAction action)
    {
        if (action != ApplicationProxyAction.Retire)
        {
            return null;
        }
        if (request.Headers[GrantRevocationRules.KeyHeader] is not { Count: 1 } keys || !GrantRevocationRules.TryKey(keys[0], out _))
        {
            throw new ApplicationProxyRejected(StatusCodes.Status400BadRequest);
        }
        return keys[0];
    }

    /// <summary>Allows only a single canonical strong precondition and distinguishes missing from malformed input.</summary>
    private static string? ReadVersion(HttpRequest request, ApplicationProxyAction action)
    {
        var values = request.Headers.IfMatch;
        if (action is ApplicationProxyAction.Create or ApplicationProxyAction.CreateNative or ApplicationProxyAction.CreateBrowser or ApplicationProxyAction.CreateMachine)
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
            throw new ApplicationProxyRejected(action == ApplicationProxyAction.AssignScopes ? StatusCodes.Status400BadRequest : StatusCodes.Status412PreconditionFailed);
        }
        return values[0];
    }

    /// <summary>Parses only the selected independently validated DTO; unknown, duplicate and null fields cannot reach the API.</summary>
    private static object Validate(byte[] bytes, ApplicationProxyAction action, DateTimeOffset now)
    {
        if (action == ApplicationProxyAction.Clone)
        {
            var clone = ApplicationJson.Parse<ApplicationClone>(bytes);
            return ApplicationCloneRules.IsValid(clone, now) ? clone : throw new InvalidDataException();
        }
        else if (action == ApplicationProxyAction.AssignScopes)
        {
            var assignment = ApplicationJson.Parse<ApplicationScopeInput>(bytes);
            return CatalogueRules.IsAssignment(assignment.Scopes) ? assignment : throw new InvalidDataException();
        }
        else if (action is ApplicationProxyAction.Disable or ApplicationProxyAction.Enable or ApplicationProxyAction.Retire)
        {
            var input = ApplicationJson.Parse<ApplicationLifecycle>(bytes);
            return ApplicationLifecycleRules.Reason(input) is not null ? input : throw new InvalidDataException();
        }
        return ValidateRegistration(bytes, action, now);
    }

    /// <summary>Validates profile-specific creation, editing and credential inputs before forwarding their exact bodies.</summary>
    private static object ValidateRegistration(byte[] bytes, ApplicationProxyAction action, DateTimeOffset now)
    {
        object value;
        IReadOnlyDictionary<string, string[]> errors;
        if (action == ApplicationProxyAction.Create)
        {
            var input = ApplicationJson.Parse<ApplicationCreate>(bytes);
            value = input;
            errors = ApplicationEditorRules.Validate(input, now);
        }
        else if (action == ApplicationProxyAction.CreateNative)
        {
            var input = ApplicationJson.Parse<ApplicationNativeCreate>(bytes);
            value = input;
            errors = ApplicationEditorRules.Validate(input);
        }
        else if (action == ApplicationProxyAction.CreateBrowser)
        {
            var input = ApplicationJson.Parse<ApplicationBrowserCreate>(bytes);
            value = input;
            errors = ApplicationEditorRules.Validate(input);
        }
        else if (action == ApplicationProxyAction.CreateMachine)
        {
            var machine = ApplicationJson.Parse<ApplicationMachineCreate>(bytes);
            return MachineClientRules.IsValid(machine, now) ? machine : throw new InvalidDataException();
        }
        else if (action == ApplicationProxyAction.Edit)
        {
            var input = ApplicationJson.Parse<ApplicationEdit>(bytes);
            if (MachineClientRules.IsEdit(input))
            {
                return input;
            }
            value = input;
            errors = ApplicationEditorRules.Validate(input);
        }
        else if (action == ApplicationProxyAction.Replace)
        {
            var input = ApplicationJson.Parse<ApplicationSecretReplacement>(bytes);
            value = input;
            errors = ApplicationEditorRules.Validate(input, now);
        }
        else
        {
            throw new InvalidDataException();
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
        if (Action is not (ApplicationProxyAction.Create or ApplicationProxyAction.CreateNative or ApplicationProxyAction.CreateBrowser or ApplicationProxyAction.CreateMachine or ApplicationProxyAction.Clone))
        {
            var previous = long.Parse(Version!.AsSpan(RevisionOffset, Version!.Length - VersionPunctuation), System.Globalization.CultureInfo.InvariantCulture);
            if (Id != details.Id || previous == long.MaxValue || revision != previous + 1 || !ApplicationVersion.Matches(Version, Id!, previous))
            {
                return false;
            }
        }
        return input switch
        {
            ApplicationMachineCreate machine => revision == 1 && details.Profile == MachineClientRules.Profile && details.RedirectUris.Count == 0 &&
                details.ClientId == machine.ClientId && details.DisplayName == machine.DisplayName && SameExpiry(details, machine.CredentialExpiresAt) && details.Enabled == true,
            ApplicationBrowserCreate browser => revision == 1 && details.Profile == ApplicationEditorRules.BrowserProfile && details.Credential is null &&
                details.ClientId == browser.ClientId && details.DisplayName == browser.DisplayName && SameRedirects(details, browser.RedirectUris) &&
                details.Enabled == true && details.Origins is not null && details.Origins.SequenceEqual(browser.Origins.Order(StringComparer.Ordinal), StringComparer.Ordinal),
            ApplicationNativeCreate native => revision == 1 && details.Profile == ApplicationEditorRules.NativeProfile && details.Credential is null &&
                details.ClientId == native.ClientId && details.DisplayName == native.DisplayName && SameRedirects(details, native.RedirectUris) && details.Enabled == true,
            ApplicationCreate create => revision == 1 && details.Profile == ApplicationEditorRules.Profile && details.ClientId == create.ClientId && details.DisplayName == create.DisplayName &&
                SameRedirects(details, create.RedirectUris) && SameExpiry(details, create.CredentialExpiresAt) && details.Enabled == true,
            ApplicationClone clone => revision == 1 && details.Profile == ApplicationEditorRules.Profile && details.Id != Id && details.ClientId == clone.ClientId &&
                details.DisplayName == clone.DisplayName && SameExpiry(details, clone.CredentialExpiresAt) && details.Enabled == false,
            ApplicationEdit edit => details.DisplayName == edit.DisplayName && SameRedirects(details, edit.RedirectUris),
            ApplicationSecretReplacement replacement => SameExpiry(details, replacement.CredentialExpiresAt),
            ApplicationLifecycle when Action is ApplicationProxyAction.Disable or ApplicationProxyAction.Enable => details.Enabled == (Action == ApplicationProxyAction.Enable),
            _ => false
        };
    }

    /// <summary>Requires the exact requested assignment and one successor application revision without accepting unrelated success data.</summary>
    internal bool Matches(ApplicationScopes scopes, long revision)
    {
        if (Action != ApplicationProxyAction.AssignScopes || input is not ApplicationScopeInput assignment || Id != scopes.ApplicationId)
        {
            return false;
        }
        var previous = long.Parse(Version!.AsSpan(RevisionOffset, Version!.Length - VersionPunctuation), System.Globalization.CultureInfo.InvariantCulture);
        return previous < long.MaxValue && revision == previous + 1 && ApplicationVersion.Matches(Version, Id!, previous) &&
            scopes.Scopes.SequenceEqual(assignment.Scopes.Order(StringComparer.Ordinal), StringComparer.Ordinal);
    }

    /// <summary>Compares the exact callback set independently of native manager enumeration order.</summary>
    private static bool SameRedirects(ApplicationDetails details, IReadOnlyList<string> expected) =>
        details.RedirectUris.Order(StringComparer.Ordinal).SequenceEqual(expected.Order(StringComparer.Ordinal));

    /// <summary>Retains the operator's exact credential deadline instead of accepting a silently changed lifetime.</summary>
    private static bool SameExpiry(ApplicationDetails details, string expected) => details.Credential is { } credential &&
        credential.ExpiresAt.ToString(ExpiryFormat, System.Globalization.CultureInfo.InvariantCulture) == expected;

    /// <summary>Encodes one opaque ID as a path segment; operation suffixes are code-owned.</summary>
    private static string ResourcePath(ApplicationProxyAction action, string? id) => action == ApplicationProxyAction.Create ? string.Empty :
        action == ApplicationProxyAction.CreateMachine ? MachineClientRules.Suffix :
        action == ApplicationProxyAction.CreateBrowser ? ApplicationEditorRules.BrowserRegistrationSuffix :
        action == ApplicationProxyAction.CreateNative ? ApplicationEditorRules.NativeRegistrationSuffix :
        PathSeparator + Uri.EscapeDataString(id!) + (action switch
        {
            ApplicationProxyAction.Edit => string.Empty,
            ApplicationProxyAction.Clone => ApplicationCloneRules.Suffix,
            ApplicationProxyAction.AssignScopes => ApplicationScopeRules.Suffix,
            ApplicationProxyAction.Replace => ReplacementSuffix,
            ApplicationProxyAction.Disable => ApplicationLifecycleRules.DisableSuffix,
            ApplicationProxyAction.Enable => ApplicationLifecycleRules.EnableSuffix,
            ApplicationProxyAction.Retire => ApplicationRetirementRules.Suffix,
            _ => throw new InvalidDataException()
        });
}

/// <summary>Closes the writable transport surface to reviewed application actions; retirement has its own acceptance response.</summary>
internal enum ApplicationProxyAction
{
    Create, Edit, Replace, Disable, Enable, AssignScopes, Clone, Retire, CreateNative, CreateBrowser, CreateMachine
}

/// <summary>Contains only a finite public status, never submitted values or downstream exception details.</summary>
internal sealed class ApplicationProxyRejected(int status) : Exception
{
    internal int Status => status;
}
