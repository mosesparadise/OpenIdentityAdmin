using IdentityPlatform.Admin.Api.Features.Authentication;
using IdentityPlatform.Core.Features.ApplicationGrants;
using IdentityPlatform.Core.Features.Applications;
using IdentityPlatform.Core.Features.Identity.Authentication;
using IdentityPlatform.Infrastructure.Features.ApplicationGrants;
using IdentityPlatform.Infrastructure.Features.Applications;
using IdentityPlatform.Infrastructure.Features.Catalogue;
using IdentityPlatform.Infrastructure.Features.Identity.Governance;
using IdentityPlatform.Infrastructure.Features.Identity.ProtocolAdmission;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Persistence;

namespace IdentityPlatform.Admin.Api.Features.Applications;

/// <summary>Privately binds current authority to one immutable mutation and exact owned epoch; it never enables an API read transaction to commit.</summary>
internal sealed class ApiMutationAuthority
{
    private const string WritePermission = "applications.write";
    private const string CredentialPermission = "applications.credentials";
    private const string LifecyclePermission = "applications.disable";
    private const string ScopePermission = "scopes.write";
    private const string AudiencePermission = "audiences.write";
    private readonly ProtocolStorageScope scope;
    private readonly ProtocolAdmissionOwnership ownership;
    private readonly ApiIdentity identity;
    private readonly object command;
    private readonly string permission;
    private readonly bool fresh;
    private readonly string callback;
    private readonly ApiSourceState source;
    private readonly DateTimeOffset started;
    private DateTimeOffset deadline;
    private int consumed;
    internal DateTimeOffset Deadline => deadline;
    internal long FactorVerifiedAt => source.FactorVerifiedAt;

    /// <summary>Captures the first successful observation and original deadlines; no external constructor can fabricate an approved receipt.</summary>
    private ApiMutationAuthority(ProtocolStorageScope scope, ProtocolAdmissionOwnership ownership, ApiIdentity identity,
        object command, string callback, ApiSourceState source, DateTimeOffset deadline, string permission, bool fresh)
    {
        this.scope = scope;
        this.ownership = ownership;
        this.identity = identity;
        this.command = command;
        this.permission = permission;
        this.fresh = fresh;
        this.callback = callback;
        this.source = source;
        this.deadline = deadline;
        started = scope.Owner.Clock.GetUtcNow();
    }

    /// <summary>Admits only the action's current persisted grant and original source/native deadlines after acquiring declared locks.</summary>
    internal static async Task<ApiMutationAuthority> AcquireAsync(ProtocolStorageScope scope, ProtocolAdmissionOwnership ownership,
        ApiIdentity identity, ApplicationMutationCommand command, string callback, CancellationToken token)
    {
        var permission = Permission(command.Kind);
        var fresh = command.Kind is ApplicationMutationKind.Replace or ApplicationMutationKind.Disable or ApplicationMutationKind.Enable;
        var observation = await ObserveAsync(scope, ownership, identity, permission, fresh, callback, token);
        return new(scope, ownership, identity, command, callback, observation.Source, observation.Deadline, permission, fresh);
    }

    /// <summary>Admits only a validated grant intent with current persisted revocation permission and original fresh MFA bounds.</summary>
    internal static async Task<ApiMutationAuthority> AcquireGrantAsync(ProtocolStorageScope scope, ProtocolAdmissionOwnership ownership,
        ApiIdentity identity, GrantIntent command, string callback, CancellationToken token)
    {
        var observation = await ObserveAsync(scope, ownership, identity, GrantRevocationRules.Permission, true, callback, token);
        return new(scope, ownership, identity, command, callback, observation.Source, observation.Deadline, GrantRevocationRules.Permission, true);
    }

    /// <summary>Binds only a validated catalogue action to its fixed current permission and the original fresh-MFA deadline.</summary>
    internal static async Task<ApiMutationAuthority> AcquireCatalogueAsync(ProtocolStorageScope scope, ProtocolAdmissionOwnership ownership,
        ApiIdentity identity, CatalogueIntent command, string callback, CancellationToken token)
    {
        var permission = command.Resource switch
        {
            CatalogueResource.Scope => ScopePermission,
            CatalogueResource.Audience => AudiencePermission,
            _ => throw ProtocolStorageOwner.Unavailable()
        };
        var observation = await ObserveAsync(scope, ownership, identity, permission, true, callback, token);
        return new(scope, ownership, identity, command, callback, observation.Source, observation.Deadline, permission, true);
    }

    /// <summary>Separates direct EF execution from native manager calls, whose owned stores acquire their own non-reentrant execution leases.</summary>
    private static async Task<(ApiSourceState Source, DateTimeOffset Deadline)> ObserveAsync(ProtocolStorageScope scope,
        ProtocolAdmissionOwnership ownership, ApiIdentity identity, string permission, bool fresh, string callback, CancellationToken token)
    {
        var database = (PlatformDbContext)scope.Owner.Database;
        ownership.RequireCurrent(database, token);
        var source = await ownership.RunAsync(database, async current => await ApiSourceObservation.ReadAsync(database, identity, scope.Owner.Clock, current), token);
        var nativeExpiry = await ApiReadProfile.ReadOwnedAsync(scope, ownership, identity, callback, token);
        if (source is null || nativeExpiry is null)
        {
            throw new ApiMutationDenied(StatusCodes.Status401Unauthorized);
        }
        var grants = await ownership.RunAsync(database, async current => await new GovernanceGrants(database).ReadProtocolAsync(ownership, current), token);
        ownership.RequireCurrent(database, token);
        var deadline = new[] { identity.ExpiresAt, nativeExpiry.Value, DateTimeOffset.FromUnixTimeSeconds(source.ExpiresAt) }.Min();
        var now = scope.Owner.Clock.GetUtcNow();
        if (deadline <= now)
        {
            throw new ApiMutationDenied(StatusCodes.Status401Unauthorized);
        }
        if (!grants.Permissions(identity.Subject).Contains(permission))
        {
            throw new ApiMutationDenied(StatusCodes.Status403Forbidden);
        }
        if (fresh)
        {
            var mfaDeadline = DateTimeOffset.FromUnixTimeSeconds(checked(source.FactorVerifiedAt + AdministratorPolicy.FreshnessSeconds));
            if (mfaDeadline <= now)
            {
                throw new ApiMutationDenied(StatusCodes.Status403Forbidden);
            }
            deadline = deadline < mfaDeadline ? deadline : mfaDeadline;
        }
        return (source, deadline);
    }

    /// <summary>Selects only the named action's persisted grant; unknown actions cannot fall through to ordinary write permission.</summary>
    private static string Permission(ApplicationMutationKind kind) => kind switch
    {
        ApplicationMutationKind.Create or ApplicationMutationKind.Edit => WritePermission,
        ApplicationMutationKind.Replace => CredentialPermission,
        ApplicationMutationKind.Disable or ApplicationMutationKind.Enable => LifecyclePermission,
        _ => throw ProtocolStorageOwner.Unavailable()
    };

    /// <summary>Rechecks fresh persisted authority after native saves and audit, shortening but never renewing its original useful lifetime.</summary>
    internal async Task VerifyAsync(ProtocolStorageScope current, object intent, CancellationToken token)
    {
        Require(current, intent, token);
        var observation = await ObserveAsync(scope, ownership, identity, permission, fresh, callback, token);
        if (observation.Source.FactorVerifiedAt != source.FactorVerifiedAt || observation.Source.Binding != source.Binding)
        {
            throw new ApiMutationDenied(StatusCodes.Status401Unauthorized);
        }
        deadline = observation.Deadline < deadline ? observation.Deadline : deadline;
        Require(current, intent, token);
    }

    /// <summary>Rejects foreign scope/action substitution, replay, clock rollback and original expiry before the one commit attempt.</summary>
    private void Require(ProtocolStorageScope current, object intent, CancellationToken token)
    {
        if (!ReferenceEquals(scope, current) || !ReferenceEquals(command, intent) || Volatile.Read(ref consumed) != 0)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        ownership.RequireCurrent((PlatformDbContext)current.Owner.Database, token);
        RequireTime(current.Owner.Clock);
    }

    /// <summary>Consumes the receipt once immediately before the owning native commit; unknown commit outcomes cannot be retried with it.</summary>
    internal void Consume(ProtocolStorageScope current, object intent, CancellationToken token)
    {
        Require(current, intent, token);
        if (Interlocked.Exchange(ref consumed, 1) != 0)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
    }

    /// <summary>Uses only frozen receipt data after real child cleanup; it never calls disposed native services.</summary>
    internal void RequirePublication(TimeProvider clock, ApplicationReadBudget budget)
    {
        budget.RequireCurrent();
        if (Volatile.Read(ref consumed) != 1)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        RequireTime(clock);
    }

    /// <summary>Never extends authority on wall-clock rollback or equality at the earliest original deadline.</summary>
    private void RequireTime(TimeProvider clock)
    {
        var now = clock.GetUtcNow();
        if (now < started || now >= deadline)
        {
            throw new ApiMutationDenied(StatusCodes.Status401Unauthorized);
        }
    }
}

/// <summary>Exposes only a finite HTTP denial; no private identity, command or native exception message becomes a response.</summary>
internal sealed class ApiMutationDenied(int status) : Exception
{
    internal int Status => status;
}
