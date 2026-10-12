using IdentityPlatform.Core.Features.Identity.Authentication;
using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Infrastructure.Features.Identity.Journeys;

/// <summary>Discovers untrusted lock candidates with bounded reads; every authoritative binding is checked after locking.</summary>
internal sealed class JourneyLocator(PlatformDbContext database, ILookupNormalizer normalizer)
{
    /// <summary>Finds a candidate administrator and existing browser row without allocating any state.</summary>
    internal Task<(string? User, bool Browser)> PasswordAsync(string userName, string browserDigest, CancellationToken token) =>
        ReadAsync(async cancellation =>
        {
            var normalized = normalizer.NormalizeName(userName);
            var user = await database.Users.AsNoTracking().Where(row => row.NormalizedUserName == normalized)
                .Select(row => row.Id).SingleOrDefaultAsync(cancellation);
            var enrolled = user is not null && await database.Set<AdministratorEnrollment>().AnyAsync(row => row.UserId == user, cancellation);
            var browser = await database.Set<AdministratorBrowserBudget>().AnyAsync(row => row.Id == browserDigest, cancellation);
            return (enrolled ? user : null, browser);
        }, token);

    /// <summary>Finds only immutable locking candidates; a locator alone never proves browser ownership or eligibility.</summary>
    internal Task<(string User, string Browser)?> InteractionAsync(string publicId, CancellationToken token) =>
        ReadAsync<(string, string)?>(async cancellation =>
        {
            if (!CeremonySecrets.IsIdentifier(publicId))
            {
                return null;
            }
            var row = await database.Set<AdministratorInteraction>().AsNoTracking()
                .Where(value => value.PublicId == publicId).Select(value => new { value.PublicId, value.UserId, value.BrowserBudgetId })
                .SingleOrDefaultAsync(cancellation);
            return row?.PublicId == publicId ? (row.UserId, row.BrowserBudgetId) : null;
        }, token);

    /// <summary>Owns connection settings and a short deadline even before account serialization starts.</summary>
    private async Task<T> ReadAsync<T>(Func<CancellationToken, Task<T>> read, CancellationToken token)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(TimeSpan.FromSeconds(AdministratorPolicy.OperationSeconds));
        await using var connection = new CeremonyConnection(database);
        await connection.OpenAsync(deadline.Token);
        return await read(deadline.Token);
    }
}
