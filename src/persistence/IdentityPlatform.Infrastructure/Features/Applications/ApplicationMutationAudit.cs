using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Infrastructure.Features.Applications;

/// <summary>Stages and independently verifies finite mutation audit and action-specific provenance in the owning transaction.</summary>
internal static class ApplicationMutationAudit
{
    private const string Actor = "User";
    private const string Target = "Application";
    private const string Accepted = "Accepted";
    private const string Created = "ApplicationCreated";
    private const string Edited = "ApplicationEdited";
    private const string Replaced = "ApplicationSecretReplaced";
    private const string Disabled = "ApplicationDisabled";
    private const string Enabled = "ApplicationEnabled";
    private const string ScopesAssigned = "ApplicationScopesAssigned";

    private const string Cloned = "ApplicationCloned";

    /// <summary>Adds exactly one sanitized intent with finite action, actor, target and correlation fields in the owning database transaction.</summary>
    internal static ValueTask<ApplicationAuditStamp> AuditAsync(ProtocolStorageScope scope, ApplicationMutationCommand command, ApplicationWriteState state,
        string actor, Guid correlation, CancellationToken token) => scope.Owner.RunAsync(async current =>
    {
        var stamp = new ApplicationAuditStamp(Guid.NewGuid(), correlation, ApplicationMutationWriter.Now(scope, command));
        scope.Owner.Database.Add(new SecurityAuditIntent
        {
            Id = stamp.Id,
            Event = Event(command.Kind),
            ActorType = Actor,
            ActorId = actor,
            TargetType = Target,
            TargetId = state.Id,
            Outcome = Accepted,
            CorrelationId = stamp.Correlation,
            OccurredAt = stamp.At
        });
        if (command.Reason is { } reason)
        {
            scope.Owner.Database.Add(new ApplicationLifecycleAuditDetail { AuditId = stamp.Id, ReasonCode = reason });
        }
        if (command.Kind == ApplicationMutationKind.Clone)
        {
            scope.Owner.Database.Add(new ApplicationCloneAuditDetail
            {
                AuditId = stamp.Id,
                SourceApplicationId = command.ApplicationId!,
                SourceVersion = command.Version!
            });
        }
        await scope.Owner.Database.SaveChangesAsync(current);
        return stamp;
    }, token, write: true);

    /// <summary>Requires the exact persisted audit after internal saves rather than trusting a tracked intent that could have been suppressed or altered.</summary>
    internal static ValueTask VerifyAuditAsync(ProtocolStorageScope scope, ApplicationMutationCommand command, ApplicationWriteState state,
        string actor, ApplicationAuditStamp stamp, CancellationToken token) => scope.Owner.RunAsync(async current =>
    {
        var row = await ((PlatformDbContext)scope.Owner.Database).Set<SecurityAuditIntent>().AsNoTracking().SingleOrDefaultAsync(value => value.Id == stamp.Id, current);
        if (row is null || row.Event != Event(command.Kind) || row.ActorType != Actor || row.ActorId != actor || row.TargetType != Target ||
            row.TargetId != state.Id || row.Outcome != Accepted || row.CorrelationId != stamp.Correlation || row.OccurredAt != stamp.At || row.RelatedCorrelationId is not null)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        if (command.Reason is { } reason)
        {
            var detail = await scope.Owner.Database.Set<ApplicationLifecycleAuditDetail>().AsNoTracking().SingleOrDefaultAsync(value => value.AuditId == stamp.Id, current);
            if (detail is null || detail.ReasonCode != reason)
            {
                throw ProtocolStorageOwner.Unavailable();
            }
        }
        if (command.Kind == ApplicationMutationKind.Clone)
        {
            var detail = await scope.Owner.Database.Set<ApplicationCloneAuditDetail>().AsNoTracking().SingleOrDefaultAsync(value => value.AuditId == stamp.Id, current);
            if (detail is null || detail.SourceApplicationId != command.ApplicationId || detail.SourceVersion != command.Version)
            {
                throw ProtocolStorageOwner.Unavailable();
            }
        }
    }, token);

    /// <summary>Maps only the closed mutation action set to an audit name owned by this feature.</summary>
    private static string Event(ApplicationMutationKind kind) => kind switch
    {
        ApplicationMutationKind.Create or ApplicationMutationKind.CreateNative or ApplicationMutationKind.CreateBrowser or ApplicationMutationKind.CreateMachine => Created,
        ApplicationMutationKind.Clone => Cloned,
        ApplicationMutationKind.Edit => Edited,
        ApplicationMutationKind.Replace => Replaced,
        ApplicationMutationKind.Disable => Disabled,
        ApplicationMutationKind.Enable => Enabled,
        ApplicationMutationKind.AssignScopes => ScopesAssigned,
        _ => throw ProtocolStorageOwner.Unavailable()
    };
}
