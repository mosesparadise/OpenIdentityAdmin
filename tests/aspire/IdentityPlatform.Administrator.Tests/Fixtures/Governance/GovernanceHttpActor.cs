using IdentityPlatform.Infrastructure.Features.Identity.Journeys;

namespace IdentityPlatform.Administrator.Tests.Fixtures.Governance;

/// <summary>Holds only in-memory synthetic native credentials and genuine stored session identifiers without a secret-bearing record representation.</summary>
internal sealed class GovernanceHttpActor(JourneySessionCandidate session, string password, string seed)
{
    internal JourneySessionCandidate Session { get; } = session;
    internal string Password { get; } = password;
    internal string Seed { get; } = seed;
    internal string UserId => Session.UserId;
}
