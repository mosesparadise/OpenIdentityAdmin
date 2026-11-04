using IdentityPlatform.Infrastructure.Features.Applications;
using IdentityPlatform.Infrastructure.Features.Identity.ManagedApplications;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Administrator.Tests.Features.ManagedApplications;

/// <summary>Changes actual attachment-stage persistence after real native token creation, never replacing protocol validation.</summary>
internal sealed class ManagedCacheAttachmentFault(ManagedAttachmentDefect defect) : SaveChangesInterceptor
{
    private const int DigestLength = 64;
    private const string Failure = "Synthetic managed attachment failure.";
    internal bool Armed
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

    /// <summary>Attaches only to the actual issuer persistence context; API registration remains genuine and unaffected.</summary>
    internal void Configure(IServiceCollection services) => services.AddDbContext<PlatformDbContext>((_, options) => options.AddInterceptors(this));

    /// <summary>Mutates committed-in-transaction fields only after request attachment has actually saved.</summary>
    public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        var database = eventData.Context!;
        var row = database.ChangeTracker.Entries<ManagedLoginInteraction>().SingleOrDefault(entry => entry.Entity.RequestId is not null)?.Entity;
        if (!Armed || Fired || row is null)
        {
            return result;
        }
        Fired = true;
        var affected = 0;
        switch (defect)
        {
            case ManagedAttachmentDefect.Throw:
                throw new InvalidOperationException(Failure);
            case ManagedAttachmentDefect.Browser:
                row.BrowserDigest = new string('A', DigestLength);
                affected = await database.Set<ManagedLoginInteraction>().Where(value => value.SelectorDigest == row.SelectorDigest)
                    .ExecuteUpdateAsync(setter => setter.SetProperty(value => value.BrowserDigest, row.BrowserDigest), cancellationToken);
                break;
            case ManagedAttachmentDefect.NativeRevoked:
                affected = await database.Set<OpenIddictEntityFrameworkCoreToken>().Where(value => value.Id == row.RequestId)
                    .ExecuteUpdateAsync(setter => setter.SetProperty(value => value.Status, OpenIddictConstants.Statuses.Revoked), cancellationToken);
                break;
            case ManagedAttachmentDefect.Policy:
                affected = await database.Set<ApplicationPolicy>().Where(value => value.ApplicationId == row.ApplicationId)
                    .ExecuteUpdateAsync(setter => setter.SetProperty(value => value.Revision, value => value.Revision + 1), cancellationToken);
                break;
            case ManagedAttachmentDefect.Watermark:
                affected = await database.Set<ApplicationPolicy>().Where(value => value.ApplicationId == row.ApplicationId)
                    .ExecuteUpdateAsync(setter => setter.SetProperty(value => value.ObservedAt, row.CreatedAt - 1), cancellationToken);
                break;
            case ManagedAttachmentDefect.Expired:
                Advance!();
                affected = 1;
                break;
        }
        if (affected == 1)
        {
            MutationCompleted = true;
        }
        return result;
    }
}

/// <summary>Names independently injected failures at the real final attachment save.</summary>
public enum ManagedAttachmentDefect
{
    Throw, Browser, NativeRevoked, Policy, Watermark, Expired
}
