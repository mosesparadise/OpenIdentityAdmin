using System.Text;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.WebUtilities;

namespace IdentityPlatform.Admin.Bff.Features.BrowserSessions;

/// <summary>Owns exact transport framing before native form parsing, cookie authentication or database mutation.</summary>
internal static class BrowserLoginRequest
{
    internal const string Completion = "Completion";
    internal const string Intent = "Intent";
    private const string Authorization = "Authorization";
    private const int CookieBytes = 16384;
    private const char CookieSeparator = ';';
    private const char Assignment = '=';
    private const string TransferEncoding = "Transfer-Encoding";

    /// <summary>Rejects duplicate or aliased security cookies before ASP.NET's dictionary can collapse them.</summary>
    internal static bool Cookie(HttpRequest request, string name, out string? value)
    {
        value = null;
        var header = request.Headers.Cookie.ToString();
        if (header.Length > CookieBytes)
        {
            return false;
        }
        foreach (var field in header.Split(CookieSeparator, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var split = field.IndexOf(Assignment);
            var key = split < 0 ? field : field[..split];
            if (!string.Equals(key, name, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            if (key != name || split < 0 || value is not null || field[(split + 1)..] is not { Length: > 0 } candidate)
            {
                return false;
            }
            if (candidate.Contains('"'))
            {
                return false;
            }
            value = candidate;
        }
        return value is null || request.Cookies[name] == value;
    }

    /// <summary>Allows only an exact HTTPS GET without browser bearer authority or a request body.</summary>
    internal static bool Get(HttpRequest request) => HttpMethods.IsGet(request.Method) && request.IsHttps &&
        !request.Headers.ContainsKey(Authorization) && request.ContentLength is null or 0 &&
        !request.Headers.ContainsKey(TransferEncoding);

    /// <summary>Rejects actual GET body bytes even when a transport omits its declared content length.</summary>
    internal static async Task<bool> EmptyAsync(HttpContext context) =>
        await context.Request.Body.ReadAsync(new byte[1], context.RequestAborted) == 0;

    /// <summary>Accepts one canonical completion only; intent, generation and destinations remain protected server expectations.</summary>
    internal static string? Resume(HttpRequest request) => Get(request) && request.Query.Count == 1 &&
        request.Query.Keys.Single() == Completion && request.Query[Completion] is { Count: 1 } value &&
        BrowserLoginSelector.Digest(value[0]) is not null ? value[0] : null;

    /// <summary>Caps actual streamed bytes before native URL decoding, rejects pre-parsed bypasses and freezes the exact single-field form for antiforgery.</summary>
    internal static async Task<bool> FormAsync(HttpContext context, BrowserSessionDestinations destinations)
    {
        var request = context.Request;
        if (!BrowserRequestPolicy.Mutation(request, destinations) || context.Features.Get<IFormFeature>() is not null)
        {
            return false;
        }
        var buffer = new byte[BrowserRequestPolicy.BodyBytes + 1];
        var used = 0;
        while (used < buffer.Length)
        {
            var read = await request.Body.ReadAsync(buffer.AsMemory(used), context.RequestAborted);
            if (read == 0)
            {
                break;
            }
            used += read;
        }
        if (used > BrowserRequestPolicy.BodyBytes || request.ContentLength is { } declared && declared != used)
        {
            return false;
        }
        return await ParseAsync(context, buffer.AsMemory(0, used));
    }

    /// <summary>Uses the native form reader with independent bounded decoded cardinality and strict UTF-8 input.</summary>
    private static async Task<bool> ParseAsync(HttpContext context, ReadOnlyMemory<byte> bytes)
    {
        try
        {
            var text = new UTF8Encoding(false, true).GetString(bytes.Span);
            using var reader = new FormReader(text)
            {
                KeyLengthLimit = BrowserRequestPolicy.ValueLength,
                ValueLengthLimit = BrowserRequestPolicy.ValueLength,
                ValueCountLimit = 1
            };
            var form = new FormCollection(await reader.ReadFormAsync(context.RequestAborted));
            if (!BrowserRequestPolicy.Form(form))
            {
                return false;
            }
            context.Features.Set<IFormFeature>(new FormFeature(form));
            return true;
        }
        catch (Exception error) when (error is InvalidDataException or DecoderFallbackException) { return false; }
    }
}
