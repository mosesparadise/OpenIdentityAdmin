using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using IdentityPlatform.Infrastructure.Persistence;

namespace IdentityPlatform.Infrastructure.Features.Identity.ProtocolAdmission;

/// <summary>Retains the exact issuance epoch and ordered governance, profile and account locks through native persistence.</summary>
internal sealed class ProtocolAdmissionOwnership
{
    private readonly ProtocolStorageOwner owner;
    private readonly ProtocolStoragePhase phase;
    private readonly PlatformDbContext database;
    internal string UserId
    {
        get;
    }

    /// <summary>Constructs authority only after all declared locks have been acquired by the same active native transaction.</summary>
    private ProtocolAdmissionOwnership(ProtocolStorageOwner owner, PlatformDbContext database, string userId)
    {
        this.owner = owner;
        this.database = database;
        UserId = userId;
        phase = owner.Capture(write: true);
    }

    /// <summary>Uses the existing governance/account lock mechanism, inserting profile serialization before any account lock.</summary>
    internal static async Task<ProtocolAdmissionOwnership> AcquireAsync(ProtocolStorageOwner owner, string userId, CancellationToken token)
    {
        if (owner.Database is not PlatformDbContext database || owner.Transaction is null ||
            database.ChangeTracker.Entries().Any() || string.IsNullOrEmpty(userId) || userId.Length > FactorSchema.UserLength)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        await owner.RunAsync(cancellation => new ValueTask(new CeremonyLocks(database)
            .AcquireAsync(new CeremonyLockPlan(true, false, [], []), cancellation)), token, write: true);
        await AdministratorProtocolLocks.AcquireProfileAsync(owner, token);
        await owner.RunAsync(cancellation => new ValueTask(new CeremonyLocks(database)
            .AcquireAsync(new CeremonyLockPlan(false, false, [], [userId]), cancellation)), token, write: true);
        return new(owner, database, userId);
    }

    /// <summary>Rejects cross-context, completed, cancelled and stale observations before any admission result is used.</summary>
    internal void RequireCurrent(PlatformDbContext current, CancellationToken token)
    {
        owner.Check(phase, token);
        if (!ReferenceEquals(database, current) || owner.Transaction is null)
        {
            owner.Fail();
            throw ProtocolStorageOwner.Unavailable();
        }
    }

    /// <summary>Runs admission work under the captured phase's actual occupancy and linked cancellation, retaining slow native work until completion.</summary>
    internal ValueTask<T> RunAsync<T>(PlatformDbContext current, Func<CancellationToken, ValueTask<T>> operation, CancellationToken token)
    {
        RequireCurrent(current, token);
        return owner.RunAsync(operation, token);
    }

    /// <summary>Requires an active native execution lease before shared persistence helpers can issue database work.</summary>
    internal void RequireExecution(PlatformDbContext current, CancellationToken token)
    {
        RequireCurrent(current, token);
        owner.RequireContext(current);
    }
}
