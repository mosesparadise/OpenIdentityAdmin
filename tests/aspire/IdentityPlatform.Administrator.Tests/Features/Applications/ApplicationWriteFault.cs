using IdentityPlatform.Infrastructure.Features.Applications;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using OpenIddict.Abstractions;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Administrator.Tests.Features.Applications;

/// <summary>Injects same-transaction persisted faults after real saves; none is a production extension point or authorization replacement.</summary>
internal sealed class ApplicationWriteFault(ApplicationWriteDefect defect) : SaveChangesInterceptor
{
    private const string AuditTarget = "Application";
    private const string Failure = "Synthetic application audit persistence failure.";
    internal bool Armed
    {
        get; set;
    }
    internal bool Fired
    {
        get; private set;
    }
    internal Action? Advance
    {
        get; set;
    }

    /// <summary>Fails audit persistence only after the native aggregate was saved, proving rollback spans manager-internal saves.</summary>
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (Armed && !Fired && defect == ApplicationWriteDefect.AuditFailure && HasAudit(eventData.Context!))
        {
            Fired = true;
            throw new InvalidOperationException(Failure);
        }
        if (Armed && !Fired && defect == ApplicationWriteDefect.TrackedCredentialDrift && eventData.Context!.ChangeTracker.Entries<ApplicationPolicy>().FirstOrDefault() is { } policy)
        {
            Fired = true;
            policy.Entity.CredentialExpiresAt++;
        }
        return ValueTask.FromResult(result);
    }

    /// <summary>Changes actual database rows after policy or audit persistence, leaving the original tracked native entities deliberately stale.</summary>
    public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        var database = eventData.Context!;
        if (!Armed || Fired || defect is ApplicationWriteDefect.AuditFailure or ApplicationWriteDefect.TrackedCredentialDrift || !database.ChangeTracker.Entries<ApplicationPolicy>().Any() ||
            (defect == ApplicationWriteDefect.AuditDeleted && !HasAudit(database)))
        {
            return result;
        }
        Fired = true;
        switch (defect)
        {
            case ApplicationWriteDefect.TokenRevoked:
                await database.Set<OpenIddictEntityFrameworkCoreToken>().ExecuteUpdateAsync(update => update.SetProperty(row => row.Status, OpenIddictConstants.Statuses.Revoked), cancellationToken);
                break;
            case ApplicationWriteDefect.SourceRebound:
                await database.Set<AdministratorAuthentication>().ExecuteUpdateAsync(update => update.SetProperty(row => row.Generation, row => row.Generation + 1), cancellationToken);
                await database.Set<AdministratorSession>().ExecuteUpdateAsync(update => update.SetProperty(row => row.Generation, row => row.Generation + 1), cancellationToken);
                break;
            case ApplicationWriteDefect.SourceRevoked:
                await database.Set<AdministratorSession>().ExecuteUpdateAsync(update => update.SetProperty(row => row.Revoked, true), cancellationToken);
                break;
            case ApplicationWriteDefect.AuditDeleted:
                await database.Set<SecurityAuditIntent>().Where(row => row.TargetType == AuditTarget).ExecuteDeleteAsync(cancellationToken);
                break;
            case ApplicationWriteDefect.PolicyChanged:
                await database.Set<ApplicationPolicy>().ExecuteUpdateAsync(update => update.SetProperty(row => row.Revision, row => row.Revision + 1), cancellationToken);
                break;
            case ApplicationWriteDefect.Cancelled:
            case ApplicationWriteDefect.CredentialExpired:
                Advance!();
                break;
        }
        return result;
    }

    /// <summary>Recognizes only the new feature's actual persisted or staged audit intent.</summary>
    private static bool HasAudit(DbContext database) => database.ChangeTracker.Entries<SecurityAuditIntent>().Any(entry => entry.Entity.TargetType == AuditTarget);
}

public enum ApplicationWriteDefect
{
    AuditFailure, TokenRevoked, SourceRevoked, AuditDeleted, PolicyChanged, CredentialExpired, TrackedCredentialDrift, SourceRebound, Cancelled
}
