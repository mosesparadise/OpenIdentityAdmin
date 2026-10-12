using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Infrastructure.Features.Identity.Factors;

/// <summary>Binds native staged verification to one persisted interaction and transaction; it is not session authority.</summary>
internal sealed class VerifiedStagedFactor
{
    private const string Pending = "Pending";
    private const string Enrollment = "Enrollment";
    private const string Replacement = "Replacement";
    private const int CodeDigits = 6;
    private readonly PlatformDbContext database;
    private readonly AdministratorInteraction interaction;
    private readonly Guid transaction;
    private readonly string fingerprint;
    private readonly string stamp;
    private readonly TimeProvider clock;
    private bool consumed;
    internal string UserId
    {
        get;
    }
    internal long Generation
    {
        get;
    }
    internal string Seed
    {
        get;
    }

    /// <summary>Captures immutable verified bindings; construction is restricted to the native verification path.</summary>
    private VerifiedStagedFactor(PlatformDbContext database, AdministratorInteraction interaction, string seed, TimeProvider clock)
    {
        this.database = database;
        this.interaction = interaction;
        this.clock = clock;
        transaction = database.Database.CurrentTransaction!.TransactionId;
        fingerprint = FactorBinding.Digest(interaction);
        stamp = interaction.SecurityStamp;
        UserId = interaction.UserId;
        Generation = interaction.DestinationGeneration;
        Seed = seed;
    }

    /// <summary>Invokes the real native provider against the persisted staged selection and rechecks expiry after verification.</summary>
    internal static async Task<VerifiedStagedFactor?> VerifyAsync(PlatformDbContext database, FactorAccess access,
        UserManager<PlatformUser> users, TimeProvider clock, Guid interactionId, string code, CancellationToken cancellationToken)
    {
        if (code.Length != CodeDigits || code.Any(character => character is < '0' or > '9'))
        {
            return null;
        }
        var interaction = await database.Set<AdministratorInteraction>().SingleAsync(value => value.Id == interactionId, cancellationToken);
        using var selection = access.Select(interaction, cancellationToken);
        var user = await users.FindByIdAsync(interaction.UserId) ?? throw new FactorUnavailableException();
        RequireBinding(interaction, user, clock);
        if (!await users.VerifyTwoFactorTokenAsync(user, TokenOptions.DefaultAuthenticatorProvider, code))
        {
            return null;
        }
        RequireBinding(interaction, user, clock);
        var seed = await users.GetAuthenticatorKeyAsync(user) ?? throw new FactorUnavailableException();
        cancellationToken.ThrowIfCancellationRequested();
        return new VerifiedStagedFactor(database, interaction, seed, clock);
    }

    /// <summary>Rejects expired or changed persisted bindings and never derives a generation from request input.</summary>
    private static void RequireBinding(AdministratorInteraction interaction, PlatformUser user, TimeProvider clock)
    {
        if (interaction.Purpose is not (Enrollment or Replacement) || interaction.State != Pending || interaction.StagedSeed is null || interaction.DestinationGeneration <= 0 ||
            interaction.ExpiresAt <= clock.GetUtcNow().ToUnixTimeSeconds() ||
            !string.Equals(interaction.SecurityStamp, user.SecurityStamp, StringComparison.Ordinal))
        {
            throw new FactorUnavailableException();
        }
    }

    /// <summary>Rejects proof reuse, transaction substitution, replaced selection or expiry before installation.</summary>
    internal void RequireCurrent(PlatformDbContext current)
    {
        if (consumed || database.Entry(interaction).State != EntityState.Unchanged || !ReferenceEquals(database, current) || current.Database.CurrentTransaction?.TransactionId != transaction ||
            interaction.State != Pending || FactorBinding.Digest(interaction) != fingerprint ||
            interaction.UserId != UserId || interaction.DestinationGeneration != Generation || interaction.SecurityStamp != stamp ||
            interaction.ExpiresAt <= clock.GetUtcNow().ToUnixTimeSeconds())
        {
            throw new FactorUnavailableException();
        }
    }

    /// <summary>Consumes installation authorization for exactly the verified owner, stamp and seed.</summary>
    internal void Consume(PlatformDbContext current, PlatformUser user, string seed)
    {
        RequireCurrent(current);
        if (!string.Equals(user.Id, UserId, StringComparison.Ordinal) || user.SecurityStamp != stamp || seed != Seed)
        {
            throw new FactorUnavailableException();
        }
        consumed = true;
    }
}
