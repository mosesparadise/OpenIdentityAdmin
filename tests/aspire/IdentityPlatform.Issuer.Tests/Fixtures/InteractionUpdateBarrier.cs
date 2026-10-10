using System.Collections.Concurrent;
using System.Data.Common;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace IdentityPlatform.Issuer.Tests.Fixtures;

// Synchronize application-owned conditional transitions, never native OpenIddict redemption SQL.
public sealed partial class InteractionUpdateBarrier(string interactionId, CancellationToken deadline) : DbCommandInterceptor
{
    private readonly ConcurrentDictionary<Guid, byte> contexts = new();
    private readonly TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource firstArrival = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public int Arrivals => contexts.Count;
    public Task FirstArrival => firstArrival.Task;

    public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is { } context && command.CommandText.Contains("LoginInteraction", StringComparison.Ordinal) &&
            TransitionUpdate().IsMatch(command.CommandText) &&
            command.Parameters.Cast<DbParameter>().Any(parameter => parameter.Value is string value && value == interactionId) &&
            contexts.TryAdd(context.ContextId.InstanceId, 0))
        {
            firstArrival.TrySetResult();
            if (contexts.Count == 2)
            {
                ready.TrySetResult();
            }
            using var waiting = CancellationTokenSource.CreateLinkedTokenSource(deadline, cancellationToken);
            await ready.Task.WaitAsync(waiting.Token);
        }
        return result;
    }

    [GeneratedRegex("\\bSET\\s+(?:[^\\s.]+\\.)?[\\[\\\"]?(?:CompletedSessionId|CompletedUserId|Consumed)[\\]\\\"]?\\s*=", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex TransitionUpdate();
}
