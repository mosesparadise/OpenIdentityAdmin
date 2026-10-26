using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Passively retains bounded final Kestrel headers and reconciles every response with its actual browser or native caller.</summary>
internal sealed class BrowserResponseWitness(Action detach) : IDisposable
{
    private const int MaximumResponses = 256;
    private const int MaximumItem = 2097152;
    private const int MaximumTotal = 16777216;
    private readonly object gate = new();
    private readonly List<Response> responses = [];
    private readonly List<BrowserResponseIdentity> native = [];
    private long total;
    private int failed;

    /// <summary>Observes the last outer OnStarting callback without throwing, changing headers or affecting response publication.</summary>
    internal void Observe(HttpContext http)
    {
        try
        {
            var headers = http.Response.Headers.SelectMany(pair => pair.Value.Select(value => pair.Key + Separator + value)).ToArray();
            var cookies = http.Request.Headers.Cookie.Select(value => value ?? string.Empty).ToArray();
            var identity = BrowserResponseIdentity.Create(http.Request.Method, http.Request.GetEncodedUrl(), http.Response.StatusCode);
            var retained = headers.Concat(cookies).Concat([identity.Method, identity.Address]).ToArray();
            lock (gate)
            {
                if (responses.Count >= MaximumResponses || retained.Any(value => value.Length > MaximumItem) ||
                    (total += retained.Sum(value => (long)value.Length)) > MaximumTotal)
                {
                    failed++;
                    return;
                }
                responses.Add(new(identity, headers, cookies));
            }
        }
        catch { Interlocked.Increment(ref failed); }
    }

    /// <summary>Records an actual completed native HTTP response separately; no route-based exemption supplies caller evidence.</summary>
    internal void Native(string method, string address, int status)
    {
        try
        {
            var identity = BrowserResponseIdentity.Create(method, address, status);
            lock (gate)
            {
                if (native.Count >= MaximumResponses || identity.Method.Length > MaximumItem || identity.Address.Length > MaximumItem ||
                    (total += identity.Method.Length + identity.Address.Length) > MaximumTotal)
                {
                    failed++;
                    return;
                }
                native.Add(identity);
            }
        }
        catch { Interlocked.Increment(ref failed); }
    }

    /// <summary>Requires exact response multiplicities and disjoint caller attribution before returning private header evidence.</summary>
    internal BrowserResponseEvidence Require(IReadOnlyCollection<BrowserResponseIdentity> browser)
    {
        lock (gate)
        {
            Assert.Equal(0, Volatile.Read(ref failed));
            Assert.True(browser.Count > 0 && browser.Count <= MaximumResponses);
            var browserKeys = browser.ToHashSet();
            var overlap = native.Any(browserKeys.Contains);
            Assert.False(overlap);
            var expected = browser.Concat(native).GroupBy(value => value).ToDictionary(group => group.Key, group => group.Count());
            var actual = responses.GroupBy(value => value.Identity).ToDictionary(group => group.Key, group => group.Count());
            Assert.True(expected.Count == actual.Count && expected.All(pair => actual.GetValueOrDefault(pair.Key) == pair.Value));
            var selected = responses.Where(value => browserKeys.Contains(value.Identity)).ToArray();
            Assert.Equal(browser.Count, selected.Length);
            return new(selected.SelectMany(value => value.Headers).ToArray(), selected.SelectMany(value => value.Cookies).ToArray());
        }
    }

    /// <summary>Detaches only after the caller has joined its browser observations and completed reconciliation.</summary>
    public void Dispose() => detach();

    private sealed record Response(BrowserResponseIdentity Identity, string[] Headers, string[] Cookies);
    private const string Separator = ": ";
}

/// <summary>Privately identifies actual responses without adding a correlation header or exposing request addresses.</summary>
internal sealed record BrowserResponseIdentity(string Method, string Address, int Status)
{
    /// <summary>Canonicalizes the actual absolute address consistently at the server, browser and native client boundaries.</summary>
    internal static BrowserResponseIdentity Create(string method, string address, int status) => new(method, new Uri(address).AbsoluteUri, status);
}

/// <summary>Keeps matched final response headers and actual incoming browser cookie headers in test memory only.</summary>
internal sealed record BrowserResponseEvidence(string[] Headers, string[] Cookies);
