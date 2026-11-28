using IdentityPlatform.Core.Features.Applications;
using IdentityPlatform.Database;
using IdentityPlatform.Infrastructure.Features.Identity.ManagedApplications;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Infrastructure.Features.Applications;

/// <summary>Tracks the polling cadence of one native device transaction under its original application revision.</summary>
internal sealed class DevicePollingWindow
{
    internal const string Profile = "managed-device-v1";
    internal const int LifetimeSeconds = 600;
    internal const int IntervalStep = 5;
    private const int DigestLength = 64;
    private const string TimeCheck = "CK_DevicePollingWindow_Time";
    private const string CadenceCheck = "CK_DevicePollingWindow_Cadence";
    private static readonly string TimeSql = $@"""CreatedAt"" >= 0 AND ""CreatedAt"" <= ""ObservedAt"" AND ""ObservedAt"" <= ""NextPollAt"" AND ""NextPollAt"" <= ""ExpiresAt"" AND ""ExpiresAt"" - ""CreatedAt"" = {LifetimeSeconds}";
    private static readonly string CadenceSql = $@"""ApplicationRevision"" > 0 AND ""IntervalSeconds"" BETWEEN {IntervalStep} AND {LifetimeSeconds}";

    public string Digest { get; private set; } = string.Empty;
    public string ApplicationId { get; private set; } = string.Empty;
    public long ApplicationRevision
    {
        get; private set;
    }
    public long CreatedAt
    {
        get; private set;
    }
    public long ObservedAt
    {
        get; private set;
    }
    public long NextPollAt
    {
        get; private set;
    }
    public long ExpiresAt
    {
        get; private set;
    }
    public int IntervalSeconds
    {
        get; private set;
    }

    /// <summary>Binds the native token entry identifier to a public device registration. The retained digest contains no user or device code.</summary>
    internal static DevicePollingWindow Start(string entryId, ApplicationPolicy policy, long now)
    {
        ArgumentException.ThrowIfNullOrEmpty(entryId);
        ArgumentException.ThrowIfNullOrEmpty(policy.ApplicationId);
        ArgumentOutOfRangeException.ThrowIfNegative(now);
        if (!Current(policy, now))
        {
            throw new InvalidOperationException();
        }
        return new()
        {
            Digest = ManagedLoginRules.Digest(entryId),
            ApplicationId = policy.ApplicationId,
            ApplicationRevision = policy.Revision,
            CreatedAt = now,
            ObservedAt = now,
            ExpiresAt = checked(now + LifetimeSeconds),
            IntervalSeconds = IntervalStep,
            NextPollAt = checked(now + IntervalStep)
        };
    }

    /// <summary>Advances a live window, monotonically extending its interval on early polls. A timely poll can proceed to native device validation.</summary>
    internal DevicePollResult Poll(ApplicationPolicy policy, long now)
    {
        if (!Current(policy, now) || policy.ApplicationId != ApplicationId || policy.Revision != ApplicationRevision || now < ObservedAt || now >= ExpiresAt)
        {
            return DevicePollResult.Unavailable;
        }
        var early = now < NextPollAt;
        if (early)
        {
            IntervalSeconds = Math.Min(LifetimeSeconds, IntervalSeconds + IntervalStep);
        }
        ObservedAt = now;
        NextPollAt = now + Math.Min(IntervalSeconds, ExpiresAt - now);
        return early ? DevicePollResult.SlowDown : DevicePollResult.Admitted;
    }

    /// <summary>Requires the current enabled public device policy and its credential-free lifetime at each observation.</summary>
    private static bool Current(ApplicationPolicy policy, long now) => policy.Profile == Profile && policy.Enabled && !policy.Retired &&
        policy.Revision > 0 && policy.CredentialRevision == 0 && policy.CredentialIssuedAt is null && policy.CredentialExpiresAt is null &&
        now >= policy.ObservedAt;

    /// <summary>Uses the previous cadence and deadline in the update predicate so concurrent polls must serialize or reread.</summary>
    internal static void Configure(ModelBuilder builder, IDatabaseModelDialect dialect)
    {
        var row = builder.Entity<DevicePollingWindow>();
        row.HasKey(value => value.Digest);
        row.Property(value => value.Digest).HasMaxLength(DigestLength);
        row.Property(value => value.ApplicationId).HasMaxLength(ApplicationProjection.IdentifierLength);
        row.Property(value => value.NextPollAt).IsConcurrencyToken();
        row.Property(value => value.ObservedAt).IsConcurrencyToken();
        row.Property(value => value.IntervalSeconds).IsConcurrencyToken();
        row.HasOne<ApplicationPolicy>().WithMany().HasForeignKey(value => value.ApplicationId).OnDelete(DeleteBehavior.Restrict);
        row.HasIndex(value => value.ExpiresAt);
        row.ToTable(nameof(DevicePollingWindow), table =>
        {
            table.HasCheckConstraint(TimeCheck, dialect.Identifiers(TimeSql));
            table.HasCheckConstraint(CadenceCheck, dialect.Identifiers(CadenceSql));
        });
        if (dialect.IdentityOrdinalCollation is { } ordinal)
        {
            row.Property(value => value.Digest).UseCollation(ordinal);
            row.Property(value => value.ApplicationId).UseCollation(ordinal);
        }
    }
}

/// <summary>Only an admitted poll can proceed to the native pending, denied, or approved transaction state.</summary>
internal enum DevicePollResult
{
    Unavailable, SlowDown, Admitted
}
