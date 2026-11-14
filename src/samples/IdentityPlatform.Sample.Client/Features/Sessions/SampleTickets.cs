using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.WebUtilities;

namespace IdentityPlatform.Sample.Client.Features.Sessions;

/// <summary>Stores serialized tickets only in bounded process memory; renewal cannot revive a removed or expired session.</summary>
internal sealed class SampleTickets(TimeProvider clock) : ITicketStore
{
    private const int SessionMinutes = 5;
    private const int BrowserHandleLength = 43;

    internal const string Scheme = "sample-session";
    internal const string BrowserClaim = "sample-browser";
    private const int Capacity = 64;
    private const int TicketLimit = 65536;
    private const int HandleBytes = 32;
    private readonly Lock gate = new();
    private readonly Dictionary<string, DateTimeOffset> admissions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, StoredTicket> tickets = new(StringComparer.Ordinal);

    /// <summary>Admits the one native cookie publication owned by a consumed browser attempt; logout removes admission before clearing tickets.</summary>
    internal void Admit(string browser, DateTimeOffset expires)
    {
        lock (gate)
        {
            Prune();
            if (admissions.Count >= Capacity || !admissions.TryAdd(browser, expires))
            {
                throw new InvalidOperationException(SampleProfile.Failure);
            }
        }
    }

    /// <summary>Creates a fresh opaque handle only for a bounded ticket with a future absolute expiry.</summary>
    public Task<string> StoreAsync(AuthenticationTicket ticket)
    {
        lock (gate)
        {
            Prune();
            var stored = Serialize(ticket);
            if (!admissions.TryGetValue(stored.Browser, out var bound) || stored.Expires > bound || tickets.Count >= Capacity || tickets.Values.Any(current => current.Browser == stored.Browser))
            {
                CryptographicOperations.ZeroMemory(stored.Bytes);
                throw new InvalidOperationException(SampleProfile.Failure);
            }
            var handle = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(HandleBytes));
            tickets.Add(handle, stored);
            return Task.FromResult(handle);
        }
    }

    /// <summary>Replaces only a current ticket at its original expiry; missing or terminal state is never upserted.</summary>
    public Task RenewAsync(string key, AuthenticationTicket ticket)
    {
        lock (gate)
        {
            Prune();
            if (!tickets.TryGetValue(key, out var current))
            {
                throw new InvalidOperationException(SampleProfile.Failure);
            }
            var replacement = Serialize(ticket);
            if (replacement.Expires > current.Expires || replacement.Browser != current.Browser)
            {
                CryptographicOperations.ZeroMemory(replacement.Bytes);
                throw new InvalidOperationException(SampleProfile.Failure);
            }
            CryptographicOperations.ZeroMemory(current.Bytes);
            tickets[key] = replacement;
            return Task.CompletedTask;
        }
    }

    /// <summary>Deserializes a private copy, never exposing retained bytes or trusting an expired cookie handle.</summary>
    public Task<AuthenticationTicket?> RetrieveAsync(string key)
    {
        lock (gate)
        {
            Prune();
            return Task.FromResult(tickets.TryGetValue(key, out var current) ? TicketSerializer.Default.Deserialize(current.Bytes) : null);
        }
    }

    /// <summary>Removal is terminal and clears retained serialized bearer credentials.</summary>
    public Task RemoveAsync(string key)
    {
        lock (gate)
        {
            Remove(key);
            return Task.CompletedTask;
        }
    }

    /// <summary>Ends every ticket owned by one browser, leaving other sessions and shared configuration untouched.</summary>
    internal void RemoveBrowser(string browser)
    {
        lock (gate)
        {
            admissions.Remove(browser);
            foreach (var key in tickets.Where(item => item.Value.Browser == browser).Select(item => item.Key).ToArray())
            {
                Remove(key);
            }
        }
    }

    /// <summary>Returns the current detached ticket associated with an authenticated browser for explicit resource access.</summary>
    internal AuthenticationTicket? FindBrowser(string browser)
    {
        lock (gate)
        {
            Prune();
            var current = tickets.Values.SingleOrDefault(ticket => ticket.Browser == browser);
            return current is null ? null : TicketSerializer.Default.Deserialize(current.Bytes);
        }
    }

    /// <summary>Enforces original short-lived sample state and a single browser ownership claim before serialization.</summary>
    private StoredTicket Serialize(AuthenticationTicket ticket)
    {
        var browser = ticket.Principal.FindAll(BrowserClaim).Select(claim => claim.Value).ToArray();
        var now = clock.GetUtcNow();
        if (ticket.Properties.ExpiresUtc is not { } expires || expires <= now || expires > now.AddMinutes(SessionMinutes) ||
            browser.Length != 1 || browser[0].Length != BrowserHandleLength)
        {
            throw new InvalidOperationException(SampleProfile.Failure);
        }
        var bytes = TicketSerializer.Default.Serialize(ticket);
        if (bytes.Length > TicketLimit)
        {
            CryptographicOperations.ZeroMemory(bytes);
            throw new InvalidOperationException(SampleProfile.Failure);
        }
        return new StoredTicket(bytes, expires, browser[0]);
    }

    /// <summary>Prunes expired tickets without waiting for another request to present their cookie.</summary>
    private void Prune()
    {
        foreach (var browser in admissions.Where(item => item.Value <= clock.GetUtcNow()).Select(item => item.Key).ToArray())
        {
            admissions.Remove(browser);
        }
        foreach (var key in tickets.Where(item => item.Value.Expires <= clock.GetUtcNow()).Select(item => item.Key).ToArray())
        {
            Remove(key);
        }
    }

    /// <summary>Clears one private byte buffer under the store gate without promising erasure of immutable managed strings.</summary>
    private void Remove(string key)
    {
        if (tickets.Remove(key, out var current))
        {
            admissions.Remove(current.Browser);
            CryptographicOperations.ZeroMemory(current.Bytes);
        }
    }

    /// <summary>Private immutable ownership metadata accompanies a serialized ticket that never leaves the store.</summary>
    private sealed record StoredTicket(byte[] Bytes, DateTimeOffset Expires, string Browser);
}
