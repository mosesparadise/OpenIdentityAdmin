using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace IdentityPlatform.Administrator.Tests.Fixtures;

/// <summary>Independent RFC 6238 and length-prefix verifier oracles using published synthetic test material.</summary>
internal static class FactorOracle
{
    private const string InvalidSeed = "The setup key is not canonical Base32.";

    private const int StepSeconds = 30;
    private const int CounterBytes = 8;
    private const int OffsetMask = 15;
    private const uint SignMask = 0x7fffffff;
    private const uint Modulus = 1000000;
    private const string Digits = "D6";
    private const string PublishedSeed = "12345678901234567890";

    /// <summary>Computes the published SHA-1 TOTP without calling production factor code or Identity's provider.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA5350", Justification = "Independent RFC 6238 SHA-1 interoperability oracle for the native Identity authenticator; not password or signing cryptography.")]
    internal static string Code(long unixSeconds)
    {
        Span<byte> counter = stackalloc byte[CounterBytes];
        BinaryPrimitives.WriteInt64BigEndian(counter, unixSeconds / StepSeconds);
        var digest = HMACSHA1.HashData(Encoding.ASCII.GetBytes(PublishedSeed), counter);
        var offset = digest[^1] & OffsetMask;
        return ((BinaryPrimitives.ReadUInt32BigEndian(digest.AsSpan(offset)) & SignMask) % Modulus).ToString(Digits, CultureInfo.InvariantCulture);
    }

    /// <summary>Computes independent native-compatible TOTP for the manager-generated Base32 enrollment key.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA5350", Justification = "Independent RFC 6238 SHA-1 interoperability oracle for native authenticator verification.")]
    internal static string Code(string seed, long unixSeconds)
    {
        const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        const int SymbolBits = 5;
        const int ByteBits = 8;
        var output = new List<byte>();
        var bits = 0;
        var buffer = 0;
        foreach (var symbol in seed)
        {
            var value = Alphabet.IndexOf(symbol, StringComparison.Ordinal);
            Assert.True(value >= 0, InvalidSeed);
            buffer = (buffer << SymbolBits) | value;
            bits += SymbolBits;
            if (bits >= ByteBits)
            {
                bits -= ByteBits;
                output.Add((byte)(buffer >> bits));
            }
        }
        Span<byte> counter = stackalloc byte[CounterBytes];
        BinaryPrimitives.WriteInt64BigEndian(counter, unixSeconds / StepSeconds);
        var key = output.ToArray();
        var digest = HMACSHA1.HashData(key, counter);
        CryptographicOperations.ZeroMemory(key);
        var offset = digest[^1] & OffsetMask;
        return ((BinaryPrimitives.ReadUInt32BigEndian(digest.AsSpan(offset)) & SignMask) % Modulus).ToString(Digits, CultureInfo.InvariantCulture);
    }

    /// <summary>Chooses a canonical value outside the native window plus the bounded request interval on either side.</summary>
    internal static string InvalidCode(string seed)
    {
        const int FirstAcceptedOffset = -4;
        const int AcceptedCounterCount = 9;
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var accepted = Enumerable.Range(FirstAcceptedOffset, AcceptedCounterCount)
            .Select(offset => Code(seed, now + offset * StepSeconds)).ToHashSet(StringComparer.Ordinal);
        var candidate = 0;
        while (accepted.Contains(candidate.ToString(Digits, CultureInfo.InvariantCulture)))
        {
            candidate++;
        }
        return candidate.ToString(Digits, CultureInfo.InvariantCulture);
    }

    /// <summary>Checks every native counter crossed by the actual bounded request; a timing escape fails evidence without retrying authentication.</summary>
    internal static void RequireWindow(string seed, string code, long started, bool valid)
    {
        const int MaximumIntervalSeconds = 20;
        const int FirstOffset = -2;
        const int WindowCount = 5;
        const string InvalidInterval = "The native factor request escaped its observed bounded clock interval.";
        const string InvalidWindow = "The independent code did not retain its intended validity throughout the native request interval.";
        var ended = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        Assert.True(ended >= started && ended - started <= MaximumIntervalSeconds, InvalidInterval);
        for (var counter = started / StepSeconds; counter <= ended / StepSeconds; counter++)
        {
            var accepted = Enumerable.Range(FirstOffset, WindowCount).Select(offset => Code(seed, (counter + offset) * StepSeconds));
            Assert.True(accepted.Contains(code, StringComparer.Ordinal) == valid, InvalidWindow);
        }
    }

    /// <summary>Selects a distinct value within the native adjacent-step window without reusing an accepted numeric code.</summary>
    internal static string UnusedCode(string seed, string accepted)
        => UnusedCode(seed, new HashSet<string>([accepted], StringComparer.Ordinal));

    /// <summary>Excludes every already accepted numeric value while selecting an independently computed bounded native-window code.</summary>
    internal static string UnusedCode(string seed, IReadOnlySet<string> accepted)
    {
        const string NoUnusedCode = "The native window contained no distinct independent code.";
        const int NativeWindowSteps = 2;
        int[] offsets = [StepSeconds, -StepSeconds, StepSeconds * NativeWindowSteps, -StepSeconds * NativeWindowSteps];
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var codes = offsets.Select(offset => Code(seed, now + offset));
        var selected = codes.FirstOrDefault(code => !accepted.Contains(code));
        Assert.True(selected is not null, NoUnusedCode);
        return selected!;
    }

    /// <summary>Builds the documented wire representation independently of the production incremental hash implementation.</summary>
    internal static string Verifier(string userId, long generation, string code)
    {
        const string Domain = "IdentityPlatform.AdministratorRecovery.v1";
        const int PrefixBytes = 4;
        using var stream = new MemoryStream();
        foreach (var segment in new[] { Domain, userId, generation.ToString(CultureInfo.InvariantCulture), code })
        {
            var bytes = Encoding.UTF8.GetBytes(segment);
            var prefix = new byte[PrefixBytes];
            BinaryPrimitives.WriteInt32BigEndian(prefix, bytes.Length);
            stream.Write(prefix);
            stream.Write(bytes);
        }
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }
}
