using System.Globalization;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Migrator.Features.AdministratorProtocol.Upgrade;

/// <summary>Binds explicit reconciliation to the single atomic audit receipt, never merely to a browser marker.</summary>
internal static class AdministratorUpgradeReceipt
{
    private const string Event = "administrator-browser-upgrade";
    private const string Actor = "deployment-operator";
    private const string Target = "administrator-protocol-profile";
    private const string Identifier = "1";
    private const string OutcomePrefix = "upgraded-to:";
    private const int DuplicateLimit = 2;

    /// <summary>Reads at most two correlation matches under profile ownership and compares strings ordinally outside provider collation.</summary>
    internal static ValueTask<SecurityAuditIntent[]> ReadAsync(ProtocolStorageOwner owner, Guid operation, CancellationToken token) =>
        owner.RunAsync(current => new ValueTask<SecurityAuditIntent[]>(owner.Database.Set<SecurityAuditIntent>().AsNoTracking()
            .Where(row => row.CorrelationId == operation).Take(DuplicateLimit).ToArrayAsync(current)), token);

    /// <summary>The fixed event means exactly one revision increment; the bounded outcome stores its canonical destination.</summary>
    internal static bool Matches(SecurityAuditIntent[] rows, AdministratorRegistrationInput input) => rows is [var row] &&
        row.Event == Event && row.ActorType == Actor && row.ActorId == input.OperatorReference && row.TargetType == Target &&
        row.TargetId == Identifier && row.Outcome == Outcome(input) && row.CorrelationId == input.OperationId && row.OccurredAt >= 0;

    /// <summary>Stages only sanitized deployment provenance and the committed target revision in the owning transaction.</summary>
    internal static SecurityAuditIntent Create(AdministratorRegistrationInput input, long observed) => new()
    {
        Event = Event,
        ActorType = Actor,
        ActorId = input.OperatorReference,
        TargetType = Target,
        TargetId = Identifier,
        Outcome = Outcome(input),
        CorrelationId = input.OperationId,
        OccurredAt = observed
    };

    /// <summary>Fits the existing 32-character audit outcome even for the largest legal target revision.</summary>
    private static string Outcome(AdministratorRegistrationInput input) => OutcomePrefix + checked(input.ExpectedRevision!.Value + 1).ToString(CultureInfo.InvariantCulture);
}
