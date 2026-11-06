using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Playwright;

namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Retains only canonical identifiers, finite validation results and digests, never raw request bodies or arbitrary header values.</summary>
internal sealed record GrantRecoveryIntent(string Url, Guid Key, string Version, string Body, string? JobVersion, bool Valid)
{
    private const string KeyHeader = "idempotency-key";
    private const string VersionHeader = "if-match";
    private const string GuidFormat = "D";
    private const string Reason = "reason";
    private const string Security = "security";
    private const string Confirm = "confirm";
    private const string Job = "jobVersion";
    private const int CreationFields = 2;
    private const int ContinuationFields = 3;

    /// <summary>Checks allowed fields before retaining finite evidence; malformed input is represented by one false flag without exception text.</summary>
    internal static GrantRecoveryIntent Read(IRequest request)
    {
        var keyText = request.Headers.GetValueOrDefault(KeyHeader);
        var valid = Guid.TryParseExact(keyText, GuidFormat, out var key) && key != Guid.Empty && key.ToString(GuidFormat) == keyText;
        string? job = null;
        try
        {
            using var body = JsonDocument.Parse(request.PostData ?? string.Empty);
            var root = body.RootElement;
            var hasJob = root.TryGetProperty(Job, out var version);
            valid &= root.GetProperty(Reason).GetString() == Security && root.GetProperty(Confirm).GetBoolean();
            valid &= root.EnumerateObject().Count() == (hasJob ? ContinuationFields : CreationFields);
            job = hasJob ? Digest(version.GetString()) : null;
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException or KeyNotFoundException)
        {
            valid = false;
        }
        return new(Digest(request.Url), key, Digest(request.Headers.GetValueOrDefault(VersionHeader)), Digest(request.PostData), job, valid);
    }

    /// <summary>Allows exact binding comparisons without retaining or formatting arbitrary browser-supplied values.</summary>
    internal static string Digest(string? value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value ?? string.Empty)));
}
