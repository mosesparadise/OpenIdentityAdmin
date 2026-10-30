using System.Security.Cryptography;
using IdentityPlatform.Core.Features.Applications;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Infrastructure.Features.Applications;

/// <summary>Stages one native aggregate and finite audit in the caller's owned transaction; this helper cannot commit or publish a response.</summary>
internal static class ApplicationMutationWriter
{
    private const int SecretBytes = 32;
    private const string PilotClient = "identityplatform-pilot";
    private const string Actor = "User";
    private const string Target = "Application";
    private const string Accepted = "Accepted";
    private const string Created = "ApplicationCreated";
    private const string Edited = "ApplicationEdited";
    private const string Replaced = "ApplicationSecretReplaced";

    /// <summary>Uses real managers for all native writes and leaves version/policy persistence inside the same original transaction.</summary>
    internal static async Task<ApplicationWriteState> StageAsync(ProtocolStorageScope scope, ApplicationMutationCommand command, CancellationToken token)
    {
        var manager = scope.Services.GetRequiredService<IOpenIddictApplicationManager>();
        if (command.Kind == ApplicationMutationKind.Create)
        {
            if (Reserved(command.ClientId!))
            {
                throw new ApplicationMutationRejected(ApplicationMutationFailure.Forbidden);
            }
            if (await manager.FindByClientIdAsync(command.ClientId!, token) is not null)
            {
                throw new ApplicationMutationRejected(ApplicationMutationFailure.Duplicate);
            }
            return await CreateAsync(scope, manager, command, token);
        }
        var application = await manager.FindByIdAsync(command.ApplicationId!, token)
            ?? throw new ApplicationMutationRejected(ApplicationMutationFailure.Missing);
        if (await manager.GetIdAsync(application, token) != command.ApplicationId)
        {
            throw new ApplicationMutationRejected(ApplicationMutationFailure.Missing);
        }
        var descriptor = new OpenIddictApplicationDescriptor();
        await manager.PopulateAsync(descriptor, application, token);
        await RequireMutableAsync(scope, command.ApplicationId!, descriptor.ClientId!, token);
        var policy = await scope.Owner.RunAsync(async current => await ((PlatformDbContext)scope.Owner.Database).Set<ApplicationPolicy>()
            .SingleOrDefaultAsync(row => row.ApplicationId == command.ApplicationId, current), token);
        if (policy is null || policy.ApplicationId != command.ApplicationId || policy.Profile != ApplicationEditorRules.Profile || !ManagedApplicationProfile.Matches(descriptor))
        {
            throw new ApplicationMutationRejected(ApplicationMutationFailure.Unsupported);
        }
        if (!ApplicationVersion.Matches(command.Version, policy.ApplicationId, policy.Revision))
        {
            throw new ApplicationMutationRejected(ApplicationMutationFailure.Stale);
        }
        return await UpdateAsync(scope, manager, application, descriptor, policy, command, token);
    }

    /// <summary>Creates the native hash first, then appends the managed lifecycle row without auto-adopting any existing registration.</summary>
    private static async Task<ApplicationWriteState> CreateAsync(ProtocolStorageScope scope, IOpenIddictApplicationManager manager,
        ApplicationMutationCommand command, CancellationToken token)
    {
        var now = Now(scope, command);
        var secret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(SecretBytes));
        var descriptor = ManagedApplicationProfile.Create(command, secret);
        var application = await manager.CreateAsync(descriptor, token);
        var id = await manager.GetIdAsync(application, token) ?? throw ProtocolStorageOwner.Unavailable();
        if (!ApplicationEditorRules.IsText(id, ApplicationProjection.IdentifierLength))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        await RequireMutableAsync(scope, id, command.ClientId!, token);
        var policy = new ApplicationPolicy
        {
            ApplicationId = id,
            Revision = 1,
            CredentialRevision = 1,
            CredentialIssuedAt = now,
            CredentialExpiresAt = command.CredentialExpiry!.Value.ToUnixTimeSeconds(),
            ObservedAt = now
        };
        var expected = new ApplicationWriteState(application, descriptor, policy, secret);
        await scope.Owner.RunAsync(async current =>
        {
            scope.Owner.Database.Add(policy);
            await scope.Owner.Database.SaveChangesAsync(current);
        }, token, write: true);
        return expected;
    }

    /// <summary>Advances one aggregate revision; only explicit replacement changes the secret and credential lifecycle.</summary>
    private static async Task<ApplicationWriteState> UpdateAsync(ProtocolStorageScope scope, IOpenIddictApplicationManager manager, object application,
        OpenIddictApplicationDescriptor descriptor, ApplicationPolicy policy, ApplicationMutationCommand command, CancellationToken token)
    {
        var now = Now(scope, command);
        if (now < policy.ObservedAt)
        {
            throw new ApplicationMutationRejected(ApplicationMutationFailure.Expired);
        }
        string? secret = null;
        policy.Revision = checked(policy.Revision + 1);
        policy.ObservedAt = now;
        if (command.Kind == ApplicationMutationKind.Replace)
        {
            secret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(SecretBytes));
            descriptor.ClientSecret = secret;
            policy.CredentialRevision = checked(policy.CredentialRevision + 1);
            policy.CredentialIssuedAt = now;
            policy.CredentialExpiresAt = command.CredentialExpiry!.Value.ToUnixTimeSeconds();
        }
        else
        {
            descriptor.DisplayName = command.DisplayName;
            descriptor.RedirectUris.Clear();
            descriptor.RedirectUris.UnionWith(command.Redirects.Select(value => new Uri(value)));
        }
        var expected = new ApplicationWriteState(application, descriptor, policy, secret);
        await manager.UpdateAsync(application, descriptor, token);
        return expected;
    }

    /// <summary>Rechecks the exact operator-provided expiry under locks; no default or retry can silently extend it.</summary>
    internal static long Now(ProtocolStorageScope scope, ApplicationMutationCommand command)
    {
        var now = scope.Owner.Clock.GetUtcNow();
        if (command.CredentialExpiry is { } expiry && expiry <= now)
        {
            throw new ApplicationMutationRejected(ApplicationMutationFailure.Expired);
        }
        return now.ToUnixTimeSeconds();
    }

    /// <summary>Protects both fixed client names and persisted system application identities even when native names have drifted.</summary>
    internal static async Task RequireMutableAsync(ProtocolStorageScope scope, string id, string clientId, CancellationToken token)
    {
        if (Reserved(clientId))
        {
            throw new ApplicationMutationRejected(ApplicationMutationFailure.Forbidden);
        }
        var bound = await scope.Owner.RunAsync(async current =>
        {
            var database = (PlatformDbContext)scope.Owner.Database;
            return await database.Set<AdministratorProtocolProfile>().AsNoTracking().AnyAsync(row => row.ApplicationId == id, current) ||
                await database.Set<PilotManifest>().AsNoTracking().AnyAsync(row => row.ApplicationId == id, current);
        }, token);
        if (bound)
        {
            throw new ApplicationMutationRejected(ApplicationMutationFailure.Forbidden);
        }
    }

    /// <summary>Rejects case aliases of reserved registrations independently of provider collation.</summary>
    private static bool Reserved(string clientId) => string.Equals(clientId, AdministratorProtocolIdentity.ClientId, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(clientId, PilotClient, StringComparison.OrdinalIgnoreCase);

    /// <summary>Adds exactly one sanitized intent with finite action, actor, target and correlation fields in the owning database transaction.</summary>
    internal static ValueTask<ApplicationAuditStamp> AuditAsync(ProtocolStorageScope scope, ApplicationMutationCommand command, ApplicationWriteState state,
        string actor, Guid correlation, CancellationToken token) => scope.Owner.RunAsync(async current =>
    {
        var stamp = new ApplicationAuditStamp(Guid.NewGuid(), correlation, Now(scope, command));
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
    }, token);

    /// <summary>Maps only the closed mutation action set to an audit name owned by this feature.</summary>
    private static string Event(ApplicationMutationKind kind) => kind switch
    {
        ApplicationMutationKind.Create => Created,
        ApplicationMutationKind.Edit => Edited,
        ApplicationMutationKind.Replace => Replaced,
        _ => throw ProtocolStorageOwner.Unavailable()
    };
}

/// <summary>Freezes the nonsecret audit identity and time for exact post-save comparison.</summary>
internal sealed record ApplicationAuditStamp(Guid Id, Guid Correlation, long At);
