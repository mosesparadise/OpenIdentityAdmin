using IdentityPlatform.Core.Features.Identity.Authentication;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;

namespace IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;

/// <summary>Binds a replay-reserved native verification to the original transaction and account security state.</summary>
internal sealed class CeremonyProof
{
    private readonly CeremonyTransaction transaction;
    private readonly string userId;
    private readonly string? stamp;
    private readonly long authenticationGeneration;
    private readonly CeremonyInteraction interaction;
    private readonly string binding;
    private bool consumed;
    internal long Generation
    {
        get;
    }
    internal long VerifiedAt
    {
        get;
    }
    internal VerifiedStagedFactor? Staged
    {
        get;
    }

    /// <summary>Captures a proof only after the private factory has completed the actual replay-reserved verification.</summary>
    private CeremonyProof(CeremonyTransaction transaction, CeremonyAccount account, long generation, long verifiedAt, VerifiedStagedFactor? staged, CeremonyInteraction interaction)
    {
        this.transaction = transaction;
        this.interaction = interaction;
        binding = FactorBinding.Digest(interaction.Row);
        userId = account.User.Id;
        stamp = account.User.SecurityStamp;
        authenticationGeneration = account.Authentication.Generation;
        Generation = generation;
        VerifiedAt = verifiedAt;
        Staged = staged;
    }

    /// <summary>Constructs proof exclusively from the sealed native-verification and replay-reservation mechanism.</summary>
    internal static async Task<CeremonyProof?> VerifyAsync(CeremonyReplay replay, CeremonyTransaction transaction, CeremonyAccount account,
        CeremonyInteraction interaction, string code, bool staged)
    {
        var result = await replay.ReserveAsync(transaction, account, interaction, code, staged);
        return result is { } verified ? new CeremonyProof(transaction, account, verified.Generation, verified.VerifiedAt, verified.Staged, interaction) : null;
    }

    /// <summary>Rejects cross-transaction, stale-state or stale-time proof reuse before sensitive writes.</summary>
    internal void RequireCurrent(CeremonyTransaction current, CeremonyAccount account, long now)
    {
        interaction.RequireCurrent(current, account, now);
        if (consumed || FactorBinding.Digest(interaction.Row) != binding || !CeremonyInteraction.IsCurrent(interaction.Row, account, now) || !ReferenceEquals(current, transaction) || account.User.Id != userId || account.User.SecurityStamp != stamp ||
            account.Authentication.Generation != authenticationGeneration || now < VerifiedAt ||
            now - VerifiedAt >= AdministratorPolicy.FreshnessSeconds)
        {
            throw new FactorUnavailableException();
        }
    }
    /// <summary>Consumes complete-ceremony authorization once; later action reuse cannot reset another budget.</summary>
    internal void Consume(CeremonyTransaction current, CeremonyAccount account, long now)
    {
        RequireCurrent(current, account, now);
        consumed = true;
    }

}
