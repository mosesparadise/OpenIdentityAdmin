using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.ManagedApplications;

/// <summary>Witnesses independently durable input consumption before failing an actual late issuance/audit save.</summary>
internal sealed class ManagedIssuanceSaveFault : SaveChangesInterceptor
{
    private const string Failure = "Synthetic late managed issuance failure.";
    internal string? Event
    {
        get; set;
    }
    internal Func<Task>? Witness
    {
        get; set;
    }
    internal bool Fired
    {
        get; private set;
    }

    /// <summary>Registers on the genuine issuer-owned provider context without replacing native handlers or stores.</summary>
    internal void Configure(IServiceCollection services) => services.AddDbContext<PlatformDbContext>((_, options) => options.AddInterceptors(this));

    /// <summary>Waits for a separate committed-row read after native issuance and the real final audit save have completed.</summary>
    public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        if (Event is null || Fired || !eventData.Context!.ChangeTracker.Entries<SecurityAuditIntent>().Any(entry => entry.Entity.Event == Event))
        {
            return result;
        }
        await Witness!();
        Fired = true;
        throw new InvalidOperationException(Failure);
    }
}
