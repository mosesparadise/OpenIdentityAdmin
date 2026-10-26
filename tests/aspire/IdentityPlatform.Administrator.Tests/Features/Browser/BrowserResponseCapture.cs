using System.Collections.Concurrent;
using IdentityPlatform.Administrator.Tests.Features.BrowserSessions;
using Microsoft.Playwright;

namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Joins actual browser response identities with passive final server headers, without inventing browser observations.</summary>
internal sealed class BrowserResponseCapture
{
    private readonly ConcurrentBag<BrowserResponseIdentity> responses = [];
    private int count;
    private long total;
    private int failed;

    /// <summary>Records only an actual response event; bounded request ownership and completion remain with the traffic ledger.</summary>
    internal void Observe(IResponse response) => Observe(BrowserResponseIdentity.Create(response.Request.Method, response.Url, response.Status));

    /// <summary>Bounds every retained identity independently; excess acquisition remains fatal even when the partial multiset would match.</summary>
    internal void Observe(BrowserResponseIdentity identity)
    {
        if (Interlocked.Increment(ref count) > MaximumResponses || identity.Method.Length > MaximumItem || identity.Address.Length > MaximumItem ||
            Interlocked.Add(ref total, identity.Method.Length + identity.Address.Length) > MaximumTotal)
        {
            Interlocked.Increment(ref failed);
            return;
        }
        responses.Add(identity);
    }

    /// <summary>Reconciles both callers before supplying the actual incoming browser cookie headers to strict transient capture.</summary>
    internal void Supply(BrowserResponseWitness witness, BrowserFlowSecrets flow)
    {
        foreach (var cookie in Require(witness).Cookies)
        {
            flow.RequestHeader(Cookie, cookie, witnessed: true);
        }
    }

    /// <summary>Requires the complete response multiset instead of excluding any unmatched endpoint by its route.</summary>
    internal BrowserResponseEvidence Require(BrowserResponseWitness witness)
    {
        Assert.Equal(0, Volatile.Read(ref failed));
        return witness.Require(responses.ToArray());
    }

    /// <summary>Proves real response-only acquisition and detection while independently requiring that the deleted canary was never returned.</summary>
    internal void RequireProbe(BrowserResponseWitness witness, string canary)
    {
        var evidence = Require(witness);
        var emitted = evidence.Headers.Any(value => BrowserSecretOracle.Contains(value, [canary]));
        var returned = evidence.Cookies.Any(value => BrowserSecretOracle.Contains(value, [canary]));
        Assert.True(emitted);
        Assert.False(returned);
        Assert.Throws<Xunit.Sdk.FalseException>(() => BrowserSecretOracle.RequireAbsent(evidence.Headers, [canary]));
    }
    private const string Cookie = "cookie";
    private const int MaximumResponses = 256;
    private const int MaximumItem = 2097152;
    private const int MaximumTotal = 16777216;
}
