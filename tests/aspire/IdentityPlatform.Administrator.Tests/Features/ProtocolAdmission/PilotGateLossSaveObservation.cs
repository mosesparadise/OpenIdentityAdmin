using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using OpenIddict.Abstractions;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;

/// <summary>Holds only an actual native Valid-to-Redeemed tracked save after child authority validation, without changing native state.</summary>
internal sealed class PilotGateLossSaveObservation(PilotGateLossProbe probe, PilotGateLossStage stage) : SaveChangesInterceptor
{
    /// <summary>Proves native validation reached its real tracked code update before exposing the independent bulk-revocation race.</summary>
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (!probe.Armed || stage != PilotGateLossStage.DuringConsumption || eventData.Context is not { } database)
        {
            return result;
        }
        var entries = database.ChangeTracker.Entries<OpenIddictEntityFrameworkCoreToken>()
            .Where(entry => entry.State == EntityState.Modified && entry.Entity.Type == OpenIddictConstants.TokenTypeIdentifiers.Private.AuthorizationCode &&
                entry.Entity.Status == OpenIddictConstants.Statuses.Redeemed).ToArray();
        if (entries.Length == 0)
        {
            return result;
        }
        try
        {
            var code = Assert.Single(entries);
            var persistedStatus = await database.Set<OpenIddictEntityFrameworkCoreToken>().AsNoTracking()
                .Where(row => row.Id == code.Entity.Id).Select(row => row.Status).SingleAsync(cancellationToken);
            Assert.Equal(OpenIddictConstants.Statuses.Valid, persistedStatus);
            Assert.NotNull(code.Entity.RedemptionDate);
            await probe.HoldChildAsync(database);
            return result;
        }
        catch (Exception error)
        {
            probe.ReportFailure(error);
            throw;
        }
    }
}
