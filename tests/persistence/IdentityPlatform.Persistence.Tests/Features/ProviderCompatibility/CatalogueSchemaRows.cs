using IdentityPlatform.Infrastructure.Features.Catalogue;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Persistence.Tests.Features.ProviderCompatibility;

/// <summary>Seeds maximum-width synthetic deletion evidence independently of production input rules.</summary>
internal static class CatalogueSchemaRows
{
    internal const long Created = 1000;
    internal const long ApprovalUntil = 1300;
    internal const long NextRevision = 2;
    internal const int TargetLength = 100;
    internal const int ActorLength = 450;
    internal const int StampLength = 256;
    internal const int DigestLength = 64;
    private const string Kind = "audience";
    private const string Event = "SyntheticCatalogueSchemaWitness";
    private const string Actor = "User";
    private const string Accepted = "Accepted";

    /// <summary>Adds a queued row and real admission audit without asserting actual administrator authorization.</summary>
    internal static CatalogueDeletion Add(DbContext database)
    {
        var audit = Audit();
        var row = new CatalogueDeletion
        {
            Id = Guid.NewGuid(),
            Kind = Kind,
            TargetId = new string('t', TargetLength),
            TargetRevision = 1,
            ActorId = new string('u', ActorLength),
            SecurityStamp = new string('s', StampLength),
            AuthenticationGeneration = 1,
            FactorGeneration = 1,
            EnrollmentRevision = 0,
            FactorVerifiedAt = Created,
            CreatedAt = Created,
            ObservedAt = Created,
            ApprovalUntil = ApprovalUntil,
            RequestKey = Guid.NewGuid(),
            ScopedKeyHash = Guid.NewGuid().ToString().PadRight(DigestLength, 'a'),
            RequestHash = new string('b', DigestLength),
            AdmissionAuditId = audit.Id
        };
        database.AddRange(audit, row);
        return row;
    }

    /// <summary>Creates a separate terminal audit; shared admission/completion identities are deliberately not reused.</summary>
    internal static SecurityAuditIntent Audit() => new()
    {
        Event = Event,
        ActorType = Actor,
        TargetType = Kind,
        Outcome = Accepted,
        OccurredAt = Created
    };
}
