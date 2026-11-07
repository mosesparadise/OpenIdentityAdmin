using System.ComponentModel.DataAnnotations;
using IdentityPlatform.Core.Features.Applications;

namespace IdentityPlatform.Core.Features.ApplicationGrants;

/// <summary>Confirms a finite revocation reason; continuation also binds the last observed job version.</summary>
/// <example><code>var request = new GrantRevocation("security", true); // Send with application If-Match and a new Idempotency-Key.</code></example>
public sealed record GrantRevocation(
    [property: Required, MaxLength(ApplicationLifecycleRules.MaximumReasonLength)] string Reason,
    bool Confirm,
    [property: MaxLength(GrantRevocationRules.MaximumVersionLength)] string? JobVersion = null);

/// <summary>Names durable job states without treating a running flag as database ownership.</summary>
/// <example><code>var completed = state == GrantJobState.Succeeded;</code></example>
public enum GrantJobState
{
    Queued = 1, Processing = 2, Stopped = 3, Succeeded = 4
}

/// <summary>Exposes bounded failure categories without native exception text or private account metadata.</summary>
/// <example><code>var requiresRecovery = failure != GrantJobFailure.None;</code></example>
public enum GrantJobFailure
{
    None = 0, Authority = 1, Target = 2, Storage = 3, Clock = 4
}

/// <summary>Validates canonical request identities and explicit confirmation before persistence or forwarding.</summary>
/// <example><code>if (!GrantRevocationRules.TryKey(header, out var key)) return Results.BadRequest();</code></example>
public static class GrantRevocationRules
{
    public const string Permission = "grants.revoke";
    public const string ReadPermission = "applications.read";
    public const string KeyHeader = "Idempotency-Key";
    public const string RouteSuffix = "/grant-revocations";
    public const string ContinueSuffix = "/continue";
    public const int MaximumVersionLength = 86;
    public const int RecentLimit = 10;
    private const string GuidFormat = "D";

    /// <summary>Rejects empty, uppercase, unhyphenated, padded and alternate-form keys so retries have one wire identity.</summary>
    /// <example><code>var valid = GrantRevocationRules.TryKey("7bc74601-f6e2-4932-92a0-a6044816c151", out var key);</code></example>
    public static bool TryKey(string? value, out Guid key) =>
        Guid.TryParseExact(value, GuidFormat, out key) && key != Guid.Empty && value == key.ToString(GuidFormat);

    /// <summary>Accepts only finite confirmed reasons and exactly the version shape required for this action.</summary>
    /// <example><code>var reason = GrantRevocationRules.Reason(new("security", true), continuation: false);</code></example>
    public static ApplicationLifecycleReason? Reason(GrantRevocation input, bool continuation) =>
        (continuation ? ApplicationVersion.IsCanonical(input.JobVersion) : input.JobVersion is null)
            ? ApplicationLifecycleRules.Reason(new(input.Reason, input.Confirm)) : null;
}
