using IdentityPlatform.Infrastructure.Features.Identity.ManagedApplications;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.ManagedApplications;

/// <summary>Injects actual internal/final persistence faults without replacing native password, MFA or protocol services.</summary>
internal sealed class ManagedHumanSaveFault : SaveChangesInterceptor
{
    private const int DigestLength = 64;
    private const string EventPrefix = "managed-";
    private const string Failure = "Synthetic managed human save failure.";
    private const string Whitespace = " ";
    private const string ForeignPurpose = "Login";
    internal ManagedHumanSaveDefect? Selected
    {
        get; set;
    }
    internal bool MutationCompleted
    {
        get; private set;
    }
    internal bool Fired
    {
        get; private set;
    }
    internal Action? Advance
    {
        get; set;
    }

    /// <summary>Registers only on the real issuer context; setup and management writes retain their ordinary services.</summary>
    internal void Configure(IServiceCollection services) => services.AddDbContext<PlatformDbContext>((_, options) => options.AddInterceptors(this));

    /// <summary>Arms one independently witnessed mutation for the next explicit phase attempt.</summary>
    internal void Arm(ManagedHumanSaveDefect defect)
    {
        Selected = defect;
        Fired = false;
        MutationCompleted = false;
    }

    /// <summary>Mutates after a real Save, distinguishing native internal saves from the final atomic managed event save.</summary>
    public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        var database = eventData.Context!;
        var row = database.ChangeTracker.Entries<ManagedLoginInteraction>().SingleOrDefault()?.Entity;
        var final = database.ChangeTracker.Entries<SecurityAuditIntent>().Any(entry => entry.Entity.Event.StartsWith(EventPrefix, StringComparison.Ordinal));
        if (Selected is not { } defect || Fired || row is null || (defect is ManagedHumanSaveDefect.InternalBinding or ManagedHumanSaveDefect.InternalPolicy or ManagedHumanSaveDefect.InternalLegacy or ManagedHumanSaveDefect.Ceremony) == final)
        {
            return result;
        }
        Fired = true;
        await MutateAsync(database, row, defect, cancellationToken);
        MutationCompleted = true;
        return result;
    }
    /// <summary>Applies a single witnessed mutation after the real save without widening production transaction authority.</summary>
    private async Task MutateAsync(DbContext database, ManagedLoginInteraction row, ManagedHumanSaveDefect defect, CancellationToken cancellationToken)
    {
        switch (defect)
        {
            case ManagedHumanSaveDefect.Throw:
                throw new InvalidOperationException(Failure);
            case ManagedHumanSaveDefect.Expired:
                Advance!();
                break;
            case ManagedHumanSaveDefect.InternalBinding:
            case ManagedHumanSaveDefect.FinalBinding:
                row.BrowserDigest = new string(row.BrowserDigest[0] == 'A' ? 'B' : 'A', DigestLength);
                Assert.Equal(1, await database.Set<ManagedLoginInteraction>().Where(value => value.SelectorDigest == row.SelectorDigest)
                    .ExecuteUpdateAsync(setter => setter.SetProperty(value => value.BrowserDigest, row.BrowserDigest), cancellationToken));
                break;
            case ManagedHumanSaveDefect.InternalLegacy:
            case ManagedHumanSaveDefect.FinalLegacy:
            case ManagedHumanSaveDefect.InternalPolicy:
            case ManagedHumanSaveDefect.FinalPolicy:
                await PolicyAsync(database, row, defect, cancellationToken);
                break;
            case ManagedHumanSaveDefect.FinalReference:
                var scopeId = await database.Set<OpenIddict.EntityFrameworkCore.Models.OpenIddictEntityFrameworkCoreScope>()
                    .Select(value => value.Id).FirstAsync(cancellationToken);
                database.Add(new ManagedScopeReference { SelectorDigest = row.SelectorDigest, ScopeId = scopeId! });
                await database.SaveChangesAsync(cancellationToken);
                break;
            case ManagedHumanSaveDefect.Ceremony:
                var ceremony = Assert.Single(database.ChangeTracker.Entries<AdministratorInteraction>()).Entity;
                ceremony.Purpose = ForeignPurpose;
                Assert.Equal(1, await database.Set<AdministratorInteraction>().Where(value => value.Id == ceremony.Id)
                    .ExecuteUpdateAsync(setter => setter.SetProperty(value => value.Purpose, ForeignPurpose), cancellationToken));
                break;
        }
    }

    /// <summary>Distinguishes malformed canonical bytes from a constraint-valid downgrade that only the immutable original binding can reject.</summary>
    private static async Task PolicyAsync(DbContext database, ManagedLoginInteraction row, ManagedHumanSaveDefect defect, CancellationToken token)
    {
        var legacy = defect is ManagedHumanSaveDefect.InternalLegacy or ManagedHumanSaveDefect.FinalLegacy;
        row.PolicySnapshot = legacy ? null : row.PolicySnapshot + Whitespace;
        row.PolicyDigest = legacy ? null : ManagedLoginRules.Digest(row.PolicySnapshot!);
        Assert.Equal(1, await database.Set<ManagedLoginInteraction>().Where(value => value.SelectorDigest == row.SelectorDigest)
            .ExecuteUpdateAsync(setter => setter.SetProperty(value => value.PolicySnapshot, row.PolicySnapshot)
                .SetProperty(value => value.PolicyDigest, row.PolicyDigest), token));
    }

}

/// <summary>Names causal faults before final snapshot adoption and after the final durable save.</summary>
public enum ManagedHumanSaveDefect
{
    InternalBinding, Ceremony, Throw, FinalBinding, Expired, InternalPolicy, FinalPolicy, FinalReference, InternalLegacy, FinalLegacy
}
