using System.Globalization;
using System.Security.Cryptography;
using IdentityPlatform.Infrastructure.Features.Identity.BrowserCompletion;
using Microsoft.AspNetCore.DataProtection;

namespace IdentityPlatform.AuthServer.Features.Accounts.Browser;

/// <summary>Protects a stable issuer-browser proof and separate immutable per-intent entry envelope without admitting durable state on GET.</summary>
internal sealed class BrowserAccountProof(IDataProtectionProvider provider, TimeProvider clock)
{
    private const string BrowserCookie = "__Host-identity-admin-issuer-browser";
    private const string EntryPrefix = "__Host-identity-admin-issuer-entry-";
    private const string BrowserPurpose = "IdentityPlatform.AuthServer.BrowserContext.v1";
    private const string EntryPurpose = "IdentityPlatform.AuthServer.BrowserEntry.v1";
    private const string Root = "/";
    private const string Separator = ":";
    private const int EnvelopeFields = 4;
    private const int CreatedIndex = 2;
    private const int ExpiresIndex = 3;
    private const int MaximumCookieLength = 2048;
    private readonly IDataProtector browserProtector = provider.CreateProtector(BrowserPurpose);
    private readonly IDataProtector entryProtector = provider.CreateProtector(EntryPurpose);

    /// <summary>Reuses an existing envelope unchanged; a changed browser proof or malformed supplied cookie never resets its original deadline.</summary>
    internal BrowserEntry? Prepare(HttpContext context, string intent)
    {
        if (IssuerContinuationPolicy.Digest(intent) is not { } digest)
        {
            return null;
        }
        if (context.Request.Cookies.ContainsKey(EntryPrefix + digest))
        {
            return Read(context, intent);
        }
        var raw = context.Request.Cookies[BrowserCookie];
        var browser = raw is null ? IssuerContinuationPolicy.CreateSelector() : Decode(browserProtector, raw);
        if (IssuerContinuationPolicy.Digest(browser) is null)
        {
            return null;
        }
        var created = clock.GetUtcNow().ToUnixTimeSeconds();
        var entry = new BrowserEntry(intent, browser!, created, checked(created + IssuerContinuationPolicy.LifetimeSeconds));
        var remaining = (long)Math.Floor((DateTimeOffset.FromUnixTimeSeconds(entry.Expires) - clock.GetUtcNow()).TotalSeconds);
        if (remaining <= 0)
        {
            return null;
        }
        context.Response.Cookies.Append(BrowserCookie, raw ?? browserProtector.Protect(browser!), Options(IssuerContinuationPolicy.LifetimeSeconds));
        var payload = string.Join(Separator, intent, browser, created.ToString(CultureInfo.InvariantCulture), entry.Expires.ToString(CultureInfo.InvariantCulture));
        context.Response.Cookies.Append(EntryPrefix + digest, entryProtector.Protect(payload), Options(remaining));
        return entry;
    }

    /// <summary>Checks the exact entry purpose, stable browser, canonical selectors and original lifetime; no browser timestamp is trusted without protection.</summary>
    internal BrowserEntry? Read(HttpContext context, string intent)
    {
        if (IssuerContinuationPolicy.Digest(intent) is not { } digest)
        {
            return null;
        }
        var fields = Decode(entryProtector, context.Request.Cookies[EntryPrefix + digest])?.Split(Separator);
        var browser = Decode(browserProtector, context.Request.Cookies[BrowserCookie]);
        if (fields is not { Length: EnvelopeFields } || fields[0] != intent || fields[1] != browser ||
            !long.TryParse(fields[CreatedIndex], NumberStyles.None, CultureInfo.InvariantCulture, out var created) ||
            !long.TryParse(fields[ExpiresIndex], NumberStyles.None, CultureInfo.InvariantCulture, out var expires))
        {
            return null;
        }
        var entry = new BrowserEntry(intent, browser!, created, expires);
        return entry.Current(clock) ? entry : null;
    }

    /// <summary>Bounds protected input and converts only cryptographic/malformed envelope failure to a fixed denial.</summary>
    private static string? Decode(IDataProtector protector, string? value)
    {
        if (value is not { Length: > 0 and <= MaximumCookieLength })
        {
            return null;
        }
        try
        {
            return protector.Unprotect(value);
        }
        catch (Exception error) when (error is CryptographicException or FormatException) { return null; }
    }

    /// <summary>Uses separate bounded host-only HttpOnly proof cookies; they grant no authenticated issuer session.</summary>
    private static CookieOptions Options(long seconds) => new()
    {
        Path = Root,
        Secure = true,
        HttpOnly = true,
        SameSite = SameSiteMode.Lax,
        MaxAge = TimeSpan.FromSeconds(seconds),
        IsEssential = true
    };
}

/// <summary>Holds transient protected entry material without a generated secret-bearing string representation.</summary>
internal sealed class BrowserEntry(string intent, string browser, long created, long expires)
{
    internal string Intent { get; } = intent;
    internal long Expires { get; } = expires;
    internal IssuerContinuationRequest? Request => IssuerContinuationRequest.Read(Intent, browser);

    /// <summary>Preserves the authenticated original entry ceiling and limited rollback tolerance through publication.</summary>
    internal bool Current(TimeProvider clock)
    {
        var now = clock.GetUtcNow().ToUnixTimeSeconds();
        return Request is not null && created > 0 && Expires > created && Expires - created == IssuerContinuationPolicy.LifetimeSeconds &&
            now >= created - IssuerContinuationPolicy.ClockToleranceSeconds && Math.Max(now, created) < Expires;
    }
}
