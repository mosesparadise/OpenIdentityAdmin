using System.Globalization;
using System.Security.Cryptography;
using IdentityPlatform.Core.Features.Identity.Authentication;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using IdentityPlatform.Infrastructure.Features.Sessions.Browser;
using Microsoft.AspNetCore.DataProtection;

namespace IdentityPlatform.Admin.Bff.Features.BrowserSessions;

/// <summary>Authenticates separate browser-generation and pending-login envelopes without treating either as a session.</summary>
internal sealed class BrowserLoginProof(IDataProtectionProvider provider)
{
    private const string Separator = ":";
    private const int GenerationCreated = 2;
    private const int GenerationExpires = 3;
    private const int PendingGeneration = 2;
    private const int PendingCreated = 3;
    private const int PendingExpires = 4;
    private const int MaximumLength = 2048;
    private const int GenerationFields = 4;
    private const int PendingFields = 5;
    private readonly IDataProtector generation = provider.CreateProtector(BrowserSessionCookies.GenerationPurpose);
    private readonly IDataProtector pending = provider.CreateProtector(BrowserSessionCookies.PreloginPurpose);

    /// <summary>Protects only a server-created generation envelope, retaining its original creation and expiry.</summary>
    internal string Protect(BrowserGenerationProof proof) => generation.Protect(string.Join(Separator, proof.Browser,
        Number(proof.Generation), Number(proof.Created), Number(proof.Expires)));

    /// <summary>Protects the original raw intent separately from the browser's generation proof and native protocol state.</summary>
    internal string Protect(BrowserPendingProof proof) => pending.Protect(string.Join(Separator, proof.Intent, proof.BrowserDigest,
        Number(proof.Generation), Number(proof.Created), Number(proof.Expires)));

    /// <summary>Accepts a finite exact-purpose canonical proof; a malformed or missing later generation never becomes initial state.</summary>
    internal BrowserGenerationProof? Generation(string? value, DateTimeOffset now)
    {
        var fields = Decode(generation, value);
        return fields is { Length: GenerationFields } && BrowserLoginSelector.Digest(fields[0]) is not null &&
            Integer(fields[1], out var number) && Integer(fields[GenerationCreated], out var created) && Integer(fields[GenerationExpires], out var expires) &&
            Current(created, expires, now.UtcTicks, AdministratorPolicy.SessionSeconds * TimeSpan.TicksPerSecond)
            ? new(fields[0], number, created, expires) : null;
    }

    /// <summary>Requires the original canonical intent, browser digest, positive generation and five-minute ceiling.</summary>
    internal BrowserPendingProof? Pending(string? value, DateTimeOffset now)
    {
        var fields = Decode(pending, value);
        return fields is { Length: PendingFields } && BrowserLoginSelector.Digest(fields[0]) is { } intent &&
            BrowserSessionPolicy.IsDigest(fields[1]) && intent != fields[1] && Integer(fields[PendingGeneration], out var number) && number > 0 &&
            Integer(fields[PendingCreated], out var created) && Integer(fields[PendingExpires], out var expires) && Current(created, expires, now.UtcTicks, BrowserSessionPolicy.Lifetime)
            ? new(fields[0], fields[1], number, created, expires) : null;
    }

    /// <summary>Bounds ciphertext before native authentication and converts only cryptographic or framing errors to denial.</summary>
    private static string[]? Decode(IDataProtector protector, string? value)
    {
        if (value is not { Length: > 0 and <= MaximumLength })
        {
            return null;
        }
        try
        {
            return protector.Unprotect(value).Split(Separator);
        }
        catch (Exception error) when (error is CryptographicException or FormatException) { return null; }
    }

    /// <summary>Rejects numeric aliases, signs and overflow instead of normalizing authenticated fields.</summary>
    private static bool Integer(string value, out long result) =>
        long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out result) && value == Number(result);

    /// <summary>Preserves expiry equality and clock rollback bounds for both immutable proof purposes.</summary>
    private static bool Current(long created, long expires, long now, long maximum) => created > 0 &&
        expires > created && expires <= DateTimeOffset.MaxValue.UtcTicks && expires - created <= maximum &&
        now >= created - BrowserSessionPolicy.Tolerance && Math.Max(created, now) < expires;

    /// <summary>Uses one invariant representation for authenticated numeric fields.</summary>
    private static string Number(long value) => value.ToString(CultureInfo.InvariantCulture);
}

/// <summary>Retains protected browser ownership without generated secret-bearing formatting.</summary>
internal sealed class BrowserGenerationProof(string browser, long generation, long created, long expires)
{
    internal string Browser { get; } = browser;
    internal string Digest => BrowserLoginSelector.Digest(Browser)!;
    internal long Generation { get; } = generation;
    internal long Created { get; } = created;
    internal long Expires { get; } = expires;
}

/// <summary>Retains the original pending intent and its independent browser/generation binding without printable credentials.</summary>
internal sealed class BrowserPendingProof(string intent, string browserDigest, long generation, long created, long expires)
{
    internal string Intent { get; } = intent;
    internal string Digest => BrowserLoginSelector.Digest(Intent)!;
    internal string BrowserDigest { get; } = browserDigest;
    internal long Generation { get; } = generation;
    internal long Created { get; } = created;
    internal long Expires { get; } = expires;
}
