using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Migrator.Features.AdministratorProtocol;

/// <summary>Creates the fixed native application, scope, manifest and product audit atomically through explicit Migrator ownership.</summary>
internal static class AdministratorRegistrationInitializer
{
    private const int OperatorLength = 64;
    private const long InitialRevision = 1;
    private const string Failure = "Administrator protocol initialization was rejected.";
    private const string Event = "administrator-protocol-initialization";
    private const string Actor = "deployment-operator";
    private const string Target = "administrator-protocol-profile";
    private const string Outcome = "created-revision-1";

    /// <summary>Uses a fresh owned context and unchanged request/write budgets; no server startup path invokes initialization.</summary>
    internal static async Task<bool> RunAsync(IServiceProvider services, AdministratorRegistrationPolicy policy, string actor, Guid operationId, CancellationToken token)
    {
        Require(operationId != Guid.Empty && actor is { Length: > 0 and <= OperatorLength } &&
            actor.All(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or '-'));
        var request = new ProtocolStorageRequest(services.GetRequiredService<TimeProvider>(), token);
        try
        {
            return await request.RunAsync(services.GetRequiredService<IServiceScopeFactory>(), async (scope, cancellation) =>
            {
                scope.Services.GetService<ProtocolStorageSelection>()?.Select(ProtocolStorageMode.Owned);
                await using var transaction = await ProtocolStorageTransaction.OpenAsync(scope.Owner);
                await LockInitializationAsync(scope.Owner, cancellation);
                if (await ExistsAsync(scope.Owner, cancellation))
                {
                    await AdministratorProtocolLocks.AcquireProfileAsync(scope.Owner, cancellation);
                    Require(await AdministratorRegistration.ReadLockedAsync(scope, policy, cancellation) is not null);
                    // A verified repeat rolls back lock-only revisions; it never repairs drift or writes another success audit.
                    return false;
                }
                await CreateAsync(scope, policy, actor, operationId, cancellation);
                await transaction.CommitAsync(cancellation);
                return true;
            });
        }
        finally { await request.DrainAsync(); }
    }

    /// <summary>Serializes absent-profile creation on existing governance coordination, preceding profile ownership in the global lock order.</summary>
    private static ValueTask LockInitializationAsync(ProtocolStorageOwner owner, CancellationToken token) =>
        owner.RunAsync(current => new ValueTask(new CeremonyLocks((PlatformDbContext)owner.Database)
            .AcquireAsync(new CeremonyLockPlan(true, false, [], []), current)), token, write: true);

    /// <summary>Reads only existence before profile locking without preloading tracked mutable authority.</summary>
    private static ValueTask<bool> ExistsAsync(ProtocolStorageOwner owner, CancellationToken token) =>
        owner.RunAsync(current => new ValueTask<bool>(owner.Database.Set<AdministratorProtocolProfile>().AnyAsync(current)), token);

    /// <summary>Rejects partial or foreign native registrations and uses native manager creates in the exact owned context.</summary>
    private static async ValueTask CreateAsync(ProtocolStorageScope scope, AdministratorRegistrationPolicy policy, string actor, Guid operationId, CancellationToken token)
    {
        var applications = scope.Services.GetRequiredService<IOpenIddictApplicationManager>();
        var scopes = scope.Services.GetRequiredService<IOpenIddictScopeManager>();
        Require(await applications.FindByClientIdAsync(AdministratorProtocolIdentity.ClientId, token) is null &&
            await scopes.FindByNameAsync(AdministratorProtocolIdentity.Scope, token) is null);
        var application = await applications.CreateAsync(policy.Application(), token);
        var resource = await scopes.CreateAsync(AdministratorRegistrationPolicy.Scope(), token);
        var applicationId = await applications.GetIdAsync(application, token);
        var scopeId = await scopes.GetIdAsync(resource, token);
        Require(!string.IsNullOrEmpty(applicationId) && !string.IsNullOrEmpty(scopeId));
        await scope.Owner.RunAsync(async current =>
        {
            var now = scope.Owner.Clock.GetUtcNow().ToUnixTimeSeconds();
            Require(now >= 0);
            AddRows(scope.Owner.Database, policy, applicationId!, scopeId!, actor, operationId, now);
            await scope.Owner.Database.SaveChangesAsync(current);
        }, token, write: true);
        Require(await AdministratorRegistration.ReadLockedAsync(scope, policy, token) is not null);
    }

    /// <summary>Stages manifest and sanitized revision audit together; no private credential or assertion enters product audit.</summary>
    private static void AddRows(DbContext database, AdministratorRegistrationPolicy policy, string applicationId, string scopeId,
        string actor, Guid operationId, long now)
    {
        database.Add(new AdministratorProtocolProfile
        {
            ApplicationId = applicationId,
            ScopeId = scopeId,
            RedirectUri = policy.RedirectUri,
            PublicKeyFingerprint = policy.Key.Fingerprint,
            Enabled = true,
            Revision = InitialRevision,
            ObservedAt = now
        });
        database.Add(new SecurityAuditIntent
        {
            Event = Event,
            ActorType = Actor,
            ActorId = actor,
            TargetType = Target,
            TargetId = AdministratorProtocolProfile.SingletonId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            Outcome = Outcome,
            CorrelationId = operationId,
            OccurredAt = now
        });
    }

    /// <summary>Reports only a finite safe outcome while preserving native rollback and actual cleanup.</summary>
    private static void Require(bool condition)
    {
        if (!condition)
        {
            throw new InvalidOperationException(Failure);
        }
    }
}
