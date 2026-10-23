using IdentityPlatform.Core.Features.Identity.Authentication;
using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Infrastructure.Features.Identity.BrowserCompletion;

/// <summary>Participates in the real password and MFA owners; it cannot construct a session or independently commit completion.</summary>
internal sealed class IssuerContinuationCeremony(PlatformDbContext database, CeremonyClock clock)
{
    /// <summary>Binds only the newly password-admitted Login interaction before its existing audit and single commit.</summary>
    internal async Task BindAsync(CeremonyTransaction transaction, AdministratorInteraction interaction, IssuerContinuationRequest request)
    {
        Require(transaction, interaction);
        var row = await database.Set<IssuerContinuation>().SingleOrDefaultAsync(row => row.IntentDigest == request.IntentDigest, transaction.Token);
        if (row is null || row.BrowserDigest != request.BrowserDigest || row.State != IssuerContinuationPolicy.Pending ||
            row.InteractionId is not null || row.CompletionDigest is not null || interaction.BrowserIntentDigest is not null ||
            !IssuerContinuationPolicy.Observe(row, clock.Now))
        {
            throw new FactorUnavailableException();
        }
        row.InteractionId = interaction.PublicId;
        row.InteractionBrowserDigest = interaction.BrowserDigest;
        row.BrowserBudgetId = interaction.BrowserBudgetId;
        row.UserId = interaction.UserId;
        row.ExpiresAt = Math.Min(row.ExpiresAt, interaction.ExpiresAt);
        row.Revision = checked(row.Revision + 1);
        interaction.BrowserIntentDigest = row.IntentDigest;
        interaction.ExpiresAt = row.ExpiresAt;
        if (!IssuerContinuationPolicy.Observe(row, clock.Now))
        {
            throw new FactorUnavailableException();
        }
    }

    /// <summary>Uses the persisted interaction marker, so ordinary completion overloads cannot bypass B3 binding or original expiry.</summary>
    internal async Task<IssuerContinuation?> ReadAsync(CeremonyTransaction transaction, AdministratorInteraction interaction)
    {
        transaction.RequireContext(database);
        if (interaction.BrowserIntentDigest is null)
        {
            if (await database.Set<IssuerContinuation>().AnyAsync(row => row.InteractionId == interaction.PublicId, transaction.Token))
            {
                throw new FactorUnavailableException();
            }
            return null;
        }
        Require(transaction, interaction);
        var row = await database.Set<IssuerContinuation>().SingleOrDefaultAsync(row => row.IntentDigest == interaction.BrowserIntentDigest, transaction.Token);
        if (row is null || row.State != IssuerContinuationPolicy.Pending || row.CompletionDigest is not null ||
            row.InteractionId != interaction.PublicId || row.InteractionBrowserDigest != interaction.BrowserDigest ||
            row.BrowserBudgetId != interaction.BrowserBudgetId || row.UserId != interaction.UserId ||
            row.ClientId != AdministratorProtocolIdentity.ClientId || row.ExpiresAt > interaction.ExpiresAt ||
            !IssuerContinuationPolicy.Observe(row, clock.Now))
        {
            throw new FactorUnavailableException();
        }
        return row;
    }

    /// <summary>Requires real login ownership with admission and account serialization; unsupported ceremonies never become browser completion.</summary>
    private void Require(CeremonyTransaction transaction, AdministratorInteraction interaction)
    {
        transaction.RequireContext(database);
        transaction.RequireCeremony();
        if (!transaction.Plan.Admission || !transaction.Plan.Users.Contains(interaction.UserId, StringComparer.Ordinal) ||
            interaction.Purpose != AdministratorPolicy.Login)
        {
            throw new FactorUnavailableException();
        }
    }
}
