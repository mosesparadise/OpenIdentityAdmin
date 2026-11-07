using IdentityPlatform.Admin.Api.Features.Applications;
using IdentityPlatform.Admin.Api.Features.Authentication;
using IdentityPlatform.Core.Features.ApplicationGrants;
using IdentityPlatform.Infrastructure.Features.ApplicationGrants;
using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.Identity.Governance;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Admin.Api.Features.ApplicationGrants;

/// <summary>Reads bounded sanitized progress under current independent permissions without requiring fresh MFA or granting mutation authority.</summary>
internal static class GrantReadOperation
{
    /// <summary>Retains rollback-only account/session/native authority and both named read permissions through actual scope disposal.</summary>
    internal static async Task<GrantJobPage> ReadAsync(IServiceScopeFactory factory, ApiIdentity identity, string applicationId,
        Guid? jobId, ApiResourceProfile profile, TimeProvider clock, CancellationToken token)
    {
        await using var scope = factory.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var database = services.GetRequiredService<PlatformDbContext>();
        await using var transaction = await CeremonyTransaction.OpenApiReadAsync(database, services.GetRequiredService<NativeOperation>(), identity.Subject, token);
        await services.GetRequiredService<ProtocolStorageSelection>().SelectApiReadAsync(transaction, database, identity.Subject);
        var access = await ApiCurrentAccess.ReadAsync(services, database, transaction, identity, profile.Callback, clock);
        if (access.Status != StatusCodes.Status200OK)
        {
            throw new ApplicationHttpRejected(access.Status);
        }
        var grants = await new GovernanceGrants(database).ReadApiAsync(transaction, identity.Subject);
        if (!grants.Permissions(identity.Subject).Contains(GrantRevocationRules.Permission))
        {
            throw new ApplicationHttpRejected(StatusCodes.Status403Forbidden);
        }
        var sourceExpiry = DateTimeOffset.FromUnixTimeSeconds(access.SourceExpiry);
        var deadline = identity.ExpiresAt < sourceExpiry ? identity.ExpiresAt : sourceExpiry;
        var target = await ApplicationDetailsReader.ReadAsync(database, applicationId, deadline, transaction.Token);
        if (target.Details is null || !target.Details.Editable)
        {
            throw new ApplicationHttpRejected(StatusCodes.Status404NotFound);
        }
        var query = database.Set<ApplicationGrantJob>().AsNoTracking().Where(row => row.ApplicationId == applicationId);
        if (jobId is { } id)
        {
            query = query.Where(row => row.Id == id);
        }
        var jobs = await query.OrderByDescending(row => row.CreatedAt).ThenByDescending(row => row.Id)
            .Take(GrantRevocationRules.RecentLimit).ToArrayAsync(transaction.Token);
        if (jobs.Any(row => row.ApplicationId != applicationId) || jobId is not null && jobs.Length != 1)
        {
            throw new ApplicationHttpRejected(StatusCodes.Status404NotFound);
        }
        transaction.RequireApiRead(database, identity.Subject);
        return new(jobs.Select(row => GrantProjection.Create(row, deadline)).ToArray(), deadline);
    }
}
