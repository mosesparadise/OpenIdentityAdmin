using System.Collections.Concurrent;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;

namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Retains actual transient browser transport values privately for later application-storage checks.</summary>
internal sealed class BrowserFlowSecrets
{
    private const string Code = "code";
    private const string State = "state";
    private const string Completion = "Completion";
    private const string Cookie = "set-cookie";
    private const string Prefix = "__Host-";
    private const string Native = "__Host-identity-admin-client-state";
    private const string Prelogin = "__Host-identity-admin-prelogin";
    private readonly ConcurrentDictionary<string, byte> values = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, byte> kinds = new(StringComparer.Ordinal);

    /// <summary>Extracts only actual native callback/completion query fields without imposing a blanket ban on their legitimate transport.</summary>
    internal void Request(string address)
    {
        var uri = new Uri(address);
        var query = QueryHelpers.ParseQuery(uri.Query);
        foreach (var name in Fields)
        {
            if (query.TryGetValue(name, out var entries))
            {
                foreach (var value in entries.Where(value => !string.IsNullOrEmpty(value)))
                {
                    values.TryAdd(value!, 0);
                    kinds.TryAdd(name, 0);
                }
            }
        }
    }

    /// <summary>Reads actual individual Set-Cookie headers, including short-lived cookies removed before the final landing.</summary>
    internal void Header(string name, string value)
    {
        if (!name.Equals(Cookie, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }
        var cookie = SetCookieHeaderValue.Parse(value);
        if (cookie.Name.Value?.StartsWith(Prefix, StringComparison.Ordinal) != true || string.IsNullOrEmpty(cookie.Value.Value))
        {
            return;
        }
        values.TryAdd(cookie.Value.Value, 0);
        if (cookie.Name.Value.StartsWith(Native, StringComparison.Ordinal))
        {
            kinds.TryAdd(Native, 0);
        }
        if (cookie.Name.Value == Prelogin)
        {
            kinds.TryAdd(Prelogin, 0);
        }
    }

    /// <summary>Requires positive capture of native code/state/completion and both transient cookie classes before any storage-absence claim.</summary>
    internal string[] Require()
    {
        Assert.True(Fields.All(kinds.ContainsKey) && kinds.ContainsKey(Native) && kinds.ContainsKey(Prelogin));
        Assert.False(values.IsEmpty);
        return values.Keys.ToArray();
    }
    private static readonly string[] Fields = [Code, State, Completion];
}
