using System.Collections.Frozen;
using IdentityPlatform.AuthServer.Features.Accounts.Shared;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;
using OpenIddict.Abstractions;

namespace IdentityPlatform.AuthServer.Features.Protocol.Administrator;

/// <summary>Requires exact, bounded browser framing before native end-session extraction.</summary>
internal static class IssuerLogoutInput
{
    internal const string Path = "/connect/end-session";
    internal const string HintName = "logout_hint";
    internal const string Confirmation = "confirmation";
    private const string FormType = "application/x-www-form-urlencoded";
    private const int MaximumBytes = 16384;
    private const int MaximumProof = 4096;
    private const int ConfirmationFields = 2;
    private const int HttpsPort = 443;
    private static readonly FrozenSet<string> Fields = new[] { OpenIddictConstants.Parameters.ClientId,
        OpenIddictConstants.Parameters.PostLogoutRedirectUri, IssuerLogoutInput.HintName,
        OpenIddictConstants.Parameters.State }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>Rejects aliases, mixed transports, credentials and ambiguous cookies before parsing native protocol fields.</summary>
    internal static async ValueTask<bool> AllowsAsync(HttpContext http, Uri issuer, CancellationToken token)
    {
        var request = http.Request;
        if (!request.IsHttps || request.PathBase.HasValue || request.Path.Value != Path ||
            request.Host.Host != issuer.Host || (request.Host.Port ?? HttpsPort) != issuer.Port ||
            request.Headers.Authorization.Count != 0 || !Cookies(request.Headers.Cookie))
        {
            return false;
        }
        if (HttpMethods.IsGet(request.Method))
        {
            return request.ContentType is null && request.ContentLength is null or 0 && request.Headers.TransferEncoding.Count == 0 &&
                request.QueryString.Value?.Length <= MaximumBytes && Allows(request.Query, post: false) &&
                await request.Body.ReadAsync(new byte[1], token) == 0;
        }
        if (!HttpMethods.IsPost(request.Method) || request.Query.Count != 0 ||
            request.Headers.Origin.Count != 1 || request.Headers.Origin[0] != issuer.GetLeftPart(UriPartial.Authority) ||
            request.ContentLength is not (> 0 and <= MaximumBytes) || request.Headers.TransferEncoding.Count != 0 ||
            !MediaTypeHeaderValue.TryParse(request.ContentType, out var media) || !media.MediaType.Equals(FormType, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        return await ReadFormAsync(http, token) is { } form && Allows(form, post: true);
    }

    /// <summary>Counts actual bytes before native parsing and replaces any pre-parsed form with the bounded original body.</summary>
    private static async Task<IFormCollection?> ReadFormAsync(HttpContext http, CancellationToken token)
    {
        if (http.Features.Get<IFormFeature>() is not null)
        {
            return null;
        }
        var bytes = new byte[MaximumBytes + 1];
        var used = 0;
        while (used < bytes.Length)
        {
            var read = await http.Request.Body.ReadAsync(bytes.AsMemory(used), token);
            if (read == 0)
            {
                break;
            }
            used += read;
        }
        if (used > MaximumBytes || http.Request.ContentLength != used)
        {
            return null;
        }
        var body = new System.Text.UTF8Encoding(false, true).GetString(bytes, 0, used);
        using var reader = new FormReader(body)
        {
            KeyLengthLimit = MaximumProof,
            ValueLengthLimit = MaximumProof,
            ValueCountLimit = Fields.Count + ConfirmationFields
        };
        var form = new FormCollection(await reader.ReadFormAsync(token));
        http.Features.Set<IFormFeature>(new FormFeature(form));
        return form;
    }

    /// <summary>Accepts exactly four protocol values and, on POST, the native antiforgery and purpose-separated confirmation.</summary>
    internal static bool Allows(IEnumerable<KeyValuePair<string, StringValues>> input, bool post)
    {
        var fields = input.ToArray();
        var allowed = post ? Fields.Append(Confirmation).Append(AdministratorBrowser.Antiforgery).ToHashSet(StringComparer.Ordinal) : Fields.ToHashSet(StringComparer.Ordinal);
        if (fields.Length != allowed.Count || fields.Select(pair => pair.Key).Distinct(StringComparer.OrdinalIgnoreCase).Count() != fields.Length ||
            fields.Any(pair => !allowed.Contains(pair.Key) || pair.Value.Count != 1 || pair.Value[0] is not { Length: > 0 and <= MaximumProof }))
        {
            return false;
        }
        var values = fields.ToDictionary(pair => pair.Key, pair => pair.Value[0], StringComparer.Ordinal);
        return values[OpenIddictConstants.Parameters.ClientId] == AdministratorProtocolIdentity.ClientId &&
            BrowserLoginSelector.Digest(values[IssuerLogoutInput.HintName]) is not null &&
            BrowserLoginSelector.Digest(values[OpenIddictConstants.Parameters.State]) is not null;
    }

    /// <summary>Rejects repeated or malformed cookie names before framework normalization can choose an authority.</summary>
    private static bool Cookies(StringValues values)
    {
        var raw = values.ToString();
        if (values.Count > 1 || raw.Length > MaximumBytes || raw.Contains(',', StringComparison.Ordinal))
        {
            return false;
        }
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        return raw.Length == 0 || raw.Split(';').All(part => part.IndexOf('=') is > 0 and var index && names.Add(part[..index].Trim()));
    }

    /// <summary>Reads the bounded custom opaque selector without interpreting it as protocol authentication.</summary>
    internal static string? Hint(OpenIddictRequest request) => (string?)request.GetParameter(HintName);

    /// <summary>Freezes exact native fields without retaining printable selector or credential representations.</summary>
    internal static string Digest(OpenIddictRequest request) => FactorBinding.Digest(request.GetParameters()
        .OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => new { pair.Key, Value = pair.Value.ToString() }).ToArray());
}
