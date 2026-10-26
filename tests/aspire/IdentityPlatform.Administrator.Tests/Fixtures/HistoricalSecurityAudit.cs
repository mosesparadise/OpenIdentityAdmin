using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Administrator.Tests.Fixtures;

/// <summary>Reads only the nine original audit fields for explicit historical-schema upgrade witnesses.</summary>
internal static class HistoricalSecurityAudit
{
    /// <summary>Preserves all original audit values without asking an older schema for the later nullable relation column.</summary>
    internal static Task<SecurityAuditIntent[]> ReadAsync(PlatformDbContext database, CancellationToken token) =>
        database.Set<SecurityAuditIntent>().AsNoTracking().OrderBy(row => row.Id).Select(row => new SecurityAuditIntent
        {
            Id = row.Id,
            Event = row.Event,
            ActorType = row.ActorType,
            ActorId = row.ActorId,
            TargetType = row.TargetType,
            TargetId = row.TargetId,
            Outcome = row.Outcome,
            CorrelationId = row.CorrelationId,
            OccurredAt = row.OccurredAt
        }).ToArrayAsync(token);
}
