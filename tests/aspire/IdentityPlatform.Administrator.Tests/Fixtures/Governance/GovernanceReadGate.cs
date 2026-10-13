using System.Data.Common;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace IdentityPlatform.Administrator.Tests.Fixtures.Governance;

/// <summary>Pauses one actual production authority read while retaining its original singleton/account locks and deadline.</summary>
internal sealed class GovernanceReadGate : DbCommandInterceptor
{
    internal const string AfterMembership = "after-membership-read";
    internal const string BeforeClaims = "before-claim-read";
    private const string GovernanceTable = "AdministratorGovernance";
    private const string MembershipTable = "AspNetUserRoles";
    private const string ClaimsTable = "AspNetRoleClaims";
    private const string Update = "UPDATE";
    private const string MissingBoundary = "The owned reader ended before the required authority-read boundary.";
    private readonly TaskCompletionSource<int> arrived = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource released = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private PlatformDbContext? owner;
    private string actor = string.Empty;
    private string role = string.Empty;
    private string boundary = string.Empty;
    private int blocker;
    private bool membershipConsumed;
    private bool triggered;
    private CancellationToken membershipToken;

    /// <summary>Targets only one explicit DbContext and exact parameter identities, excluding discovery and competing-writer queries.</summary>
    internal void Arm(PlatformDbContext database, string actorId, string roleId, string selectedBoundary)
    {
        Assert.True(selectedBoundary is AfterMembership or BeforeClaims);
        owner = database;
        actor = actorId;
        role = roleId;
        boundary = selectedBoundary;
    }

    /// <summary>Waits for the actual query event, racing owned reader completion and the caller's cancellation.</summary>
    internal async Task<int> WaitAsync(Task reader, CancellationToken token)
    {
        var first = await Task.WhenAny(arrived.Task, reader).WaitAsync(token);
        Assert.True(first == arrived.Task, MissingBoundary);
        return await arrived.Task;
    }

    /// <summary>Unblocks the original reader without altering its production transaction deadline.</summary>
    internal void Release() => released.TrySetResult();

    /// <summary>Captures the blocker identity immediately after its successful real singleton lock, before any reader is open.</summary>
    public override async ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
        int result, CancellationToken cancellationToken = default)
    {
        if (owner is not null && ReferenceEquals(eventData.Context, owner) && command.CommandText.Contains(GovernanceTable, StringComparison.Ordinal) &&
            command.CommandText.Contains(Update, StringComparison.Ordinal))
        {
            Assert.Equal(1, result);
            Assert.NotNull(owner!.Database.CurrentTransaction);
            blocker = await DatabaseWaiters.IdentifyAsync(owner, cancellationToken);
        }
        return result;
    }

    /// <summary>Retains the actual production cancellation token for the async post-consumption interception hook.</summary>
    public override ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
        DbDataReader result, CancellationToken cancellationToken = default)
    {
        if (Matches(command, eventData.Context, MembershipTable, actor))
        {
            membershipToken = cancellationToken;
        }
        return ValueTask.FromResult(result);
    }

    /// <summary>Pauses after actor membership rows were consumed, not merely after SQL dispatch or elapsed time.</summary>
    public override async ValueTask<InterceptionResult> DataReaderClosingAsync(DbCommand command,
        DataReaderClosingEventData eventData, InterceptionResult result)
    {
        if (Matches(command, eventData.Context, MembershipTable, actor))
        {
            Assert.True(eventData.ReadCount > 0);
            membershipConsumed = true;
            if (boundary == AfterMembership)
            {
                await PauseAsync(membershipToken);
            }
        }
        return result;
    }

    /// <summary>Pauses immediately before the selected role's real claim read, after completed account membership materialization.</summary>
    public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
        CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
        if (boundary == BeforeClaims && Matches(command, eventData.Context, ClaimsTable, role))
        {
            Assert.True(membershipConsumed);
            await PauseAsync(cancellationToken);
        }
        return result;
    }

    /// <summary>Identifies one exact owned transactional query without logging SQL or parameter payloads.</summary>
    private bool Matches(DbCommand command, Microsoft.EntityFrameworkCore.DbContext? context, string table, string identifier) =>
        owner is not null && ReferenceEquals(context, owner) && owner.Database.CurrentTransaction is not null &&
        command.CommandText.Contains(table, StringComparison.Ordinal) &&
        command.Parameters.Cast<DbParameter>().Any(parameter => parameter.Value is string value && value == identifier);

    /// <summary>Publishes a real locked session once and waits only within the original production cancellation lease.</summary>
    private async Task PauseAsync(CancellationToken token)
    {
        Assert.False(triggered);
        Assert.True(blocker > 0 && token.CanBeCanceled);
        triggered = true;
        arrived.TrySetResult(blocker);
        await released.Task.WaitAsync(token);
    }
}
