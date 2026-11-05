using System.Text.Json;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Infrastructure.Features.ApplicationGrants;

/// <summary>Freezes the authoritative pre-batch state before native saves can alter tracked or persisted product data.</summary>
internal sealed class GrantBaseline(GrantJobPair pair)
{
    private readonly string job = JsonSerializer.Serialize(pair.Job);
    private readonly string approval = JsonSerializer.Serialize(pair.Approval);

    /// <summary>Requires both local and freshly loaded baseline rows unchanged before deriving new progress.</summary>
    internal ValueTask VerifyAsync(ProtocolStorageScope scope, CancellationToken token) => scope.Owner.RunAsync(async current =>
    {
        var savedJob = await scope.Owner.Database.Set<ApplicationGrantJob>().AsNoTracking().SingleOrDefaultAsync(row => row.Id == pair.Job.Id, current);
        var savedApproval = await scope.Owner.Database.Set<ApplicationGrantApproval>().AsNoTracking().SingleOrDefaultAsync(row => row.Id == pair.Approval.Id, current);
        if (JsonSerializer.Serialize(pair.Job) != job || JsonSerializer.Serialize(pair.Approval) != approval ||
            JsonSerializer.Serialize(savedJob) != job || JsonSerializer.Serialize(savedApproval) != approval)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
    }, token);
}
