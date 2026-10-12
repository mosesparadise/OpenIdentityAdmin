using System.Security.Cryptography;
using System.Text.Json;

namespace IdentityPlatform.Infrastructure.Features.Identity.Factors;

/// <summary>Captures scalar persisted bindings without retaining a printable secret-bearing snapshot.</summary>
internal static class FactorBinding
{
    /// <summary>Hashes all persisted fields so scoped selections reject mutation before further native operations.</summary>
    internal static string Digest<T>(T value) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));
}
