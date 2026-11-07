using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using IdentityPlatform.Core.Features.ApplicationGrants;
using IdentityPlatform.Core.Features.Applications;

namespace IdentityPlatform.Infrastructure.Features.ApplicationGrants;

/// <summary>Freezes validated operator intent with collision-resistant canonical scope and payload digests.</summary>
internal sealed record GrantIntent(string ApplicationId, string Version, Guid RequestKey, ApplicationLifecycleReason Reason,
    Guid? JobId, string? JobVersion)
{
    private const string GuidFormat = "D";

    internal GrantApprovalAction Action => JobId is null ? GrantApprovalAction.Submit : GrantApprovalAction.Continue;

    /// <summary>Rejects unsupported targets, precondition aliases and mixed submit/continuation shapes before taking locks.</summary>
    internal static GrantIntent? Create(string id, string version, string key, GrantRevocation input, Guid? jobId = null) =>
        ApplicationEditorRules.IsText(id, ApplicationProjection.IdentifierLength) && ApplicationVersion.IsCanonical(version) &&
        GrantRevocationRules.TryKey(key, out var requestKey) && jobId != Guid.Empty &&
        GrantRevocationRules.Reason(input, jobId is not null) is { } reason
            ? new(id, version, requestKey, reason, jobId, input.JobVersion) : null;

    /// <summary>Binds the retry key to exact actor/action/target; persisted exact fields are still compared to reject collisions.</summary>
    internal string ScopeHash(string actor) => Digest([actor, Number((int)Action), ApplicationId, RequestKey.ToString(GuidFormat)]);

    /// <summary>Includes every immutable destructive input using unambiguous JSON array framing and invariant numeric formatting.</summary>
    internal string RequestHash() => Digest([ApplicationId, Version, RequestKey.ToString(GuidFormat), Number((int)Reason), JobId?.ToString(GuidFormat), JobVersion]);

    /// <summary>Creates a stable lowercase SHA256 digest; no secret or native token content enters this material.</summary>
    private static string Digest(string?[] values) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(values))));

    /// <summary>Keeps persisted intent hashes stable across the machine's current culture.</summary>
    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
}
