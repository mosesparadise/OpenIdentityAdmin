using IdentityPlatform.Admin.Api.Features.Authentication;
using IdentityPlatform.Core.Features.Applications;
using IdentityPlatform.Core.Features.Identity.Authentication;
using IdentityPlatform.Infrastructure.Features.Applications;
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
    private readonly ProtocolStorageScope scope;
    private readonly ProtocolAdmissionOwnership ownership;
    private readonly ApiIdentity identity;
    private readonly ApplicationMutationCommand command;
    private readonly string callback;
    private readonly ApiSourceState source;
    private readonly DateTimeOffset started;
    private DateTimeOffset deadline;
    private int consumed;
    internal DateTimeOffset Deadline => deadline;

    /// <summary>Captures the first successful observation and original deadlines; no external constructor can fabricate an approved receipt.</summary>
    private ApiMutationAuthority(ProtocolStorageScope scope, ProtocolAdmissionOwnership ownership, ApiIdentity identity,
        ApplicationMutationCommand command, string callback, ApiSourceState source, DateTimeOffset deadline)
    {
        this.scope = scope;
        this.ownership = ownership;
        this.identity = identity;
        this.command = command;
        this.callback = callback;
        this.source = source;
        this.deadline = deadline;
        started = scope.Owner.Clock.GetUtcNow();
    }

    /// <summary>Admits only the action's current persisted grant and original source/native deadlines after acquiring declared locks.</summary>
    internal static async Task<ApiMutationAuthority> AcquireAsync(ProtocolStorageScope scope, ProtocolAdmissionOwnership ownership,
        ApiIdentity identity, ApplicationMutationCommand command, string callback, CancellationToken token)
    {
        var observation = await ObserveAsync(scope, ownership, identity, command, callback, token);
        return new(scope, ownership, identity, command, callback, observation.Source, observation.Deadline);
    }

    /// <summary>Separates direct EF execution from native manager calls, whose owned stores acquire their own non-reentrant execution leases.</summary>
    private static async Task<(ApiSourceState Source, DateTimeOffset Deadline)> ObserveAsync(ProtocolStorageScope scope,
        ProtocolAdmissionOwnership ownership, ApiIdentity identity, ApplicationMutationCommand command, string callback, CancellationToken token)
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
        var permission = command.Kind == ApplicationMutationKind.Replace ? CredentialPermission : WritePermission;
        if (!grants.Permissions(identity.Subject).Contains(permission))
        {
            throw new ApiMutationDenied(StatusCodes.Status403Forbidden);
        }
        if (command.Kind == ApplicationMutationKind.Replace)
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

    /// <summary>Rechecks fresh persisted authority after native saves and audit, shortening but never renewing its original useful lifetime.</summary>
    internal async Task VerifyAsync(ProtocolStorageScope current, ApplicationMutationCommand intent, CancellationToken token)
    {
        Require(current, intent, token);
        var observation = await ObserveAsync(scope, ownership, identity, command, callback, token);
        if (observation.Source.FactorVerifiedAt != source.FactorVerifiedAt || observation.Source.Binding != source.Binding)
        {
            throw new ApiMutationDenied(StatusCodes.Status401Unauthorized);
        }
        deadline = observation.Deadline < deadline ? observation.Deadline : deadline;
        Require(current, intent, token);
    }

    /// <summary>Rejects foreign scope/action substitution, replay, clock rollback and original expiry before the one commit attempt.</summary>
    private void Require(ProtocolStorageScope current, ApplicationMutationCommand intent, CancellationToken token)
    {
        if (!ReferenceEquals(scope, current) || !ReferenceEquals(command, intent) || Volatile.Read(ref consumed) != 0)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        ownership.RequireCurrent((PlatformDbContext)current.Owner.Database, token);
        RequireTime(current.Owner.Clock);
    }

    /// <summary>Consumes the receipt once immediately before the owning native commit; unknown commit outcomes cannot be retried with it.</summary>
    internal void Consume(ProtocolStorageScope current, ApplicationMutationCommand intent, CancellationToken token)
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
