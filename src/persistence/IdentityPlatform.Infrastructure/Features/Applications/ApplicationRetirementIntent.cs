using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using IdentityPlatform.Core.Features.ApplicationGrants;
using IdentityPlatform.Core.Features.Applications;

namespace IdentityPlatform.Infrastructure.Features.Applications;

/// <summary>Freezes exact terminal-state intent in a replay namespace that cannot collide with ordinary grant submission or continuation.</summary>
internal sealed record ApplicationRetirementIntent(string ApplicationId, string Version, Guid RequestKey, ApplicationLifecycleReason Reason)
{
    private const string Domain = "application-retirement-v1";
    private const string GuidFormat = "D";
    internal const long ReplaySeconds = 24 * 60 * 60;

    /// <summary>Rejects malformed identities, precondition aliases, noncanonical keys and unconfirmed reasons before taking ownership.</summary>
    internal static ApplicationRetirementIntent? Create(string id, string version, string key, ApplicationLifecycle input) =>
        ApplicationEditorRules.IsText(id, ApplicationProjection.IdentifierLength) && ApplicationVersion.IsCanonical(version) &&
        GrantRevocationRules.TryKey(key, out var requestKey) && ApplicationLifecycleRules.Reason(input) is { } reason
            ? new(id, version, requestKey, reason) : null;

    /// <summary>Scopes retained keys to exact actor and application using a fixed retirement discriminator and unambiguous framing.</summary>
    internal string ScopeHash(string actor) => Digest([Domain, actor, ApplicationId, RequestKey.ToString(GuidFormat)]);

    /// <summary>Binds every confirmed destructive input without permitting replay to renew its original version or deadline.</summary>
    internal string RequestHash() => Digest([Domain, ApplicationId, Version, RequestKey.ToString(GuidFormat), ((int)Reason).ToString(CultureInfo.InvariantCulture)]);

    /// <summary>Encodes only bounded nonsecret intent fields into a stable lowercase SHA256 digest.</summary>
    private static string Digest(string[] values) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(values))));
}
