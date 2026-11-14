using System.Security.Cryptography;
using Microsoft.AspNetCore.WebUtilities;

namespace IdentityPlatform.Sample.Client.Features.Sessions;

/// <summary>Orders browser callback/output admission against logout without holding a gate during remote HTTP work.</summary>
internal sealed class BrowserAttempts(TimeProvider clock, SampleTickets tickets)
{
    private const int AttemptMinutes = 5;

    private const int Capacity = 128;
    private const int HandleBytes = 32;
    private readonly Lock gate = new();
    private readonly Dictionary<string, BrowserAttempt> browsers = new(StringComparer.Ordinal);

    /// <summary>Admits one fresh browser attempt under a hard total bound; old handles are never reused.</summary>
    internal string Begin()
    {
        lock (gate)
        {
            foreach (var key in browsers.Where(item => item.Value.Expires <= clock.GetUtcNow()).Select(item => item.Key).ToArray())
            {
                browsers.Remove(key);
                tickets.RemoveBrowser(key);
            }
            if (browsers.Count >= Capacity)
            {
                throw new InvalidOperationException(SampleProfile.Failure);
            }
            var id = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(HandleBytes));
            browsers.Add(id, new BrowserAttempt(clock.GetUtcNow().AddMinutes(AttemptMinutes)));
            return id;
        }
    }

    /// <summary>Acquires the original attempt/session gate, then rechecks dictionary membership and absolute expiry.</summary>
    internal async Task<BrowserLease?> EnterAsync(string? id, CancellationToken cancellationToken)
    {
        BrowserAttempt? attempt;
        lock (gate)
        {
            attempt = id is not null && browsers.TryGetValue(id, out var value) ? value : null;
        }
        if (attempt is null)
        {
            return null;
        }
        await attempt.Gate.WaitAsync(cancellationToken);
        lock (gate)
        {
            if (id is not null && browsers.TryGetValue(id, out var current) && ReferenceEquals(current, attempt) && attempt.Expires > clock.GetUtcNow())
            {
                return new BrowserLease(this, id, attempt);
            }
        }
        attempt.Gate.Release();
        return null;
    }

    /// <summary>Consumes pending browser authority exactly once while callback owns the publication gate.</summary>
    private void Authenticate(string id, BrowserAttempt attempt, DateTimeOffset expires)
    {
        lock (gate)
        {
            if (!browsers.TryGetValue(id, out var current) || !ReferenceEquals(current, attempt) || !attempt.Pending ||
                expires <= clock.GetUtcNow() || expires > attempt.Expires)
            {
                throw new InvalidOperationException(SampleProfile.Failure);
            }
            tickets.Admit(id, expires);
            attempt.Pending = false;
            attempt.Expires = expires;
        }
    }

    /// <summary>Removes browser authority and all owned tickets before a successful logout response can be published.</summary>
    private void End(string id, BrowserAttempt attempt)
    {
        lock (gate)
        {
            if (browsers.TryGetValue(id, out var current) && ReferenceEquals(current, attempt))
            {
                browsers.Remove(id);
                tickets.RemoveBrowser(id);
            }
        }
    }

    /// <summary>Holds one browser's output admission across a bounded final write; disposing it unblocks logout.</summary>
    internal sealed class BrowserLease(BrowserAttempts owner, string id, BrowserAttempt attempt) : IDisposable
    {
        private int disposed;
        internal bool Pending => attempt.Pending;
        internal DateTimeOffset Expires => attempt.Expires;

        /// <summary>Commits callback eligibility without extending the original browser attempt lifetime.</summary>
        internal void Authenticate(DateTimeOffset expires) => owner.Authenticate(id, attempt, expires);

        /// <summary>Ends only this browser; callers must hold the lease until final logout output completes.</summary>
        internal void End() => owner.End(id, attempt);

        /// <summary>Releases the gate exactly once, including cancellation and response failures.</summary>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) == 0)
            {
                attempt.Gate.Release();
            }
        }
    }

    /// <summary>Tracks bounded browser authority and an asynchronous output/logout gate, separate from native protocol state.</summary>
    internal sealed class BrowserAttempt(DateTimeOffset expires)
    {
        internal SemaphoreSlim Gate { get; } = new(1, 1);
        internal bool Pending { get; set; } = true;
        internal DateTimeOffset Expires { get; set; } = expires;
    }
}
