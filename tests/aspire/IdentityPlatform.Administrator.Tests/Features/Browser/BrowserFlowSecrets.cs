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
    private const string WebKitSeparator = "playwright-set-cookie-separator";
    private const string ReportFormat = "traffic-flow code={0} state={1} completion={2} native-cookie={3} prelogin-cookie={4} cookie-entries={5} cookie-parsed={6} cookie-parse-failed={7} cookie-crlf={8} cookie-separator={9} strict-cookie-groups={10} strict-group-members={11}";
    private int entries;
    private int parsed;
    private int parseFailed;
    private int crlf;
    private int separator;
    private int strictGroups;
    private int strictMembers;
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
        Interlocked.Increment(ref entries);
        if (value.Contains('\r') || value.Contains('\n'))
        {
            Interlocked.Increment(ref crlf);
        }
        if (value.Contains(WebKitSeparator, StringComparison.Ordinal))
        {
            Interlocked.Increment(ref separator);
        }
        SetCookieHeaderValue cookie;
        try
        {
            cookie = SetCookieHeaderValue.Parse(value);
            Interlocked.Increment(ref parsed);
        }
        catch (FormatException)
        {
            Interlocked.Increment(ref parseFailed);
            ProbeGroup(value);
            throw;
        }
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

    /// <summary>Classifies a rejected single-cookie value using complete native list parsing, without accepting it or retaining its contents.</summary>
    private void ProbeGroup(string value)
    {
        try
        {
            var group = SetCookieHeaderValue.ParseStrictList([value]);
            if (group.Count > 1)
            {
                Interlocked.Increment(ref strictGroups);
                Interlocked.Add(ref strictMembers, group.Count);
            }
        }
        catch (FormatException) { /* Preserve the original strict single-cookie rejection. */ }
    }

    /// <summary>Reports only fixed capture-presence flags and parsing counters, without cookie names, values or header excerpts.</summary>
    internal void Report()
    {
        try
        {
            TestContext.Current.TestOutputHelper?.WriteLine(ReportFormat, kinds.ContainsKey(Code), kinds.ContainsKey(State), kinds.ContainsKey(Completion),
                kinds.ContainsKey(Native), kinds.ContainsKey(Prelogin), Volatile.Read(ref entries), Volatile.Read(ref parsed),
                Volatile.Read(ref parseFailed), Volatile.Read(ref crlf), Volatile.Read(ref separator),
                Volatile.Read(ref strictGroups), Volatile.Read(ref strictMembers));
        }
        catch (Exception) { /* Preserve the capture verdict if its diagnostic sink fails. */ }
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
