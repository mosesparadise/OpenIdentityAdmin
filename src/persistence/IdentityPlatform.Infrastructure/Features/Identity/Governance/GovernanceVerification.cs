using IdentityPlatform.Core.Features.Identity.Authentication;
using IdentityPlatform.Core.Features.Identity.Governance;
using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.Identity.Journeys;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;

namespace IdentityPlatform.Infrastructure.Features.Identity.Governance;

/// <summary>Records an actual native credential outcome; late policy or ownership failures never become counted bad attempts.</summary>
internal sealed class GovernanceVerification
{
    private const string CredentialSavepoint = "GovernanceCredentials";
    private readonly CeremonyTransaction owner;
    private readonly string binding;
    private bool issued;
    internal bool Accepted
    {
        get;
    }
    internal GovernanceRequest Request
    {
        get;
    }
    internal CeremonyAccount Actor
    {
        get;
    }
    internal JourneySessionProof Session
    {
        get;
    }
    internal long VerifiedAt
    {
        get;
    }

    /// <summary>Captures only a completed native check and the same current full session; credentials are never retained.</summary>
    private GovernanceVerification(CeremonyTransaction owner, GovernanceRequest request, CeremonyAccount actor,
        JourneySessionProof session, bool accepted, long verifiedAt)
    {
        this.owner = owner;
        Request = request;
        Actor = actor;
        Session = session;
        Accepted = accepted;
        VerifiedAt = verifiedAt;
        binding = Binding(actor);
    }

    /// <summary>Verifies native credentials under one savepoint and discards verification side effects before a failed-attempt capability.</summary>
    internal static async Task<GovernanceVerification> VerifyAsync(PlatformDbContext database, UserManager<PlatformUser> users,
        FactorAccess access, CeremonyBudget budget, CeremonyClock clock, CeremonyReplayLedger ledger, GovernanceUsability usability,
        CeremonyTransaction transaction, GovernanceState state, JourneySessionCandidate candidate, string password, string code)
    {
        transaction.RequireContext(database);
        state.RequireAuthorized(transaction);
        access.RequireActive();
        ((AdministratorUserManager)users).RequireOperation(database, transaction.Token);
        var actor = state.Actor;
        if (!await usability.IsEligibleAsync(transaction, actor))
        {
            throw new FactorUnavailableException();
        }
        var session = await ReadSessionAsync(database, users, clock, transaction, actor, candidate);
        var native = database.Database.CurrentTransaction ?? throw new FactorUnavailableException();
        if (!native.SupportsSavepoints)
        {
            throw new FactorUnavailableException();
        }
        await native.CreateSavepointAsync(CredentialSavepoint, transaction.Token);
        var verifiedAt = await CheckAsync(users, budget, clock, ledger, transaction, actor, session, password, code);
        if (verifiedAt is null)
        {
            await native.RollbackToSavepointAsync(CredentialSavepoint, transaction.Token);
            database.ChangeTracker.Clear();
            actor = await CeremonyAccount.LoadAsync(database, transaction, candidate.UserId);
            session = await ReadSessionAsync(database, users, clock, transaction, actor, candidate);
        }
        await native.ReleaseSavepointAsync(CredentialSavepoint, transaction.Token);
        var now = Observe(clock, actor);
        session.RequireCurrent(transaction, actor, now);
        if (verifiedAt is { } original && (now < original || now - original >= AdministratorPolicy.FreshnessSeconds))
        {
            throw new FactorUnavailableException();
        }
        return new GovernanceVerification(transaction, state.Request, actor, session, verifiedAt is not null, verifiedAt ?? now);
    }

    /// <summary>Distinguishes actual native credential rejection from session invalidation, unsafe clocks and provider failures.</summary>
    private static async Task<long?> CheckAsync(UserManager<PlatformUser> users, CeremonyBudget budget, CeremonyClock clock,
        CeremonyReplayLedger ledger, CeremonyTransaction transaction, CeremonyAccount actor, JourneySessionProof session, string password, string code)
    {
        if (!await budget.OpenAsync(transaction, actor))
        {
            throw new FactorUnavailableException();
        }
        if (!await users.CheckPasswordAsync(actor.User, password))
        {
            return null;
        }
        var passwordAt = Observe(clock, actor);
        session.RequireCurrent(transaction, actor, passwordAt);
        var availability = await ledger.InspectAsync(transaction, actor, actor.Enrollment.FactorGeneration, code);
        if (availability == CeremonyReplayAvailability.Capacity)
        {
            throw new FactorUnavailableException();
        }
        if (availability != CeremonyReplayAvailability.Ready ||
            !await ledger.VerifyActiveAsync(transaction, actor, code))
        {
            session.RequireCurrent(transaction, actor, Observe(clock, actor));
            return null;
        }
        var now = Observe(clock, actor);
        session.RequireCurrent(transaction, actor, now);
        if (now < passwordAt || now - passwordAt >= AdministratorPolicy.FreshnessSeconds)
        {
            throw new FactorUnavailableException();
        }
        await ledger.ReserveAsync(transaction, actor, actor.Enrollment.FactorGeneration, code, now);
        return passwordAt;
    }

    /// <summary>Issues exactly one capability of the native outcome's kind, preventing duplicate proof wrappers.</summary>
    internal void Claim(CeremonyTransaction transaction, bool accepted, long now)
    {
        RequireCurrent(transaction, Request, now);
        if (issued || Accepted != accepted)
        {
            throw new FactorUnavailableException();
        }
        issued = true;
    }

    /// <summary>Requires the original owner, immutable intent, current state and strict freshness before either result is used.</summary>
    internal void RequireCurrent(CeremonyTransaction transaction, GovernanceRequest request, long now)
    {
        transaction.RequireGovernance();
        Actor.RequireCurrent(transaction);
        if (!ReferenceEquals(owner, transaction) || !ReferenceEquals(Request, request) || binding != Binding(Actor) ||
            now < VerifiedAt || now - VerifiedAt >= AdministratorPolicy.FreshnessSeconds)
        {
            throw new FactorUnavailableException();
        }
        Session.RequireCurrent(transaction, Actor, now);
    }

    /// <summary>Reloads the same stored session; it never refreshes or repairs bindings after native password rehash.</summary>
    private static async Task<JourneySessionProof> ReadSessionAsync(PlatformDbContext database, UserManager<PlatformUser> users,
        CeremonyClock clock, CeremonyTransaction transaction, CeremonyAccount actor, JourneySessionCandidate candidate) =>
        await JourneySessionProof.ReadAsync(database, users, clock, transaction, actor, candidate) ?? throw new FactorUnavailableException();

    /// <summary>Samples a safe account clock without treating time rejection as a bad credential.</summary>
    private static long Observe(CeremonyClock clock, CeremonyAccount account) =>
        clock.Observe(account.Authentication, out var now) ? now : throw new FactorUnavailableException();

    /// <summary>Captures native and product scalar state without storing printable credential material.</summary>
    private static string Binding(CeremonyAccount account) => FactorBinding.Digest(new { account.User, account.Enrollment, account.Authentication });
}
