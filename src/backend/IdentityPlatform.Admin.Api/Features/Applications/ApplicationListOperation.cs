using IdentityPlatform.Admin.Api.Features.Authentication;
using IdentityPlatform.Core.Features.Applications;
using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Persistence;

namespace IdentityPlatform.Admin.Api.Features.Applications;

/// <summary>Owns fresh authority and catalogue reads through actual rollback and child-scope cleanup before returning data.</summary>
internal static class ApplicationListOperation
{
    /// <summary>Uses a fresh independent native context and declared lock order; no authentication or activity changes are committed.</summary>
    internal static async Task<(int Status, ApplicationPage? Page, long SourceExpiry)> ReadAsync(IServiceScopeFactory factory,
        ApiIdentity identity, ApplicationListQuery query, ApiResourceProfile profile, TimeProvider clock, CancellationToken token)
    {
        await using var scope = factory.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var database = services.GetRequiredService<PlatformDbContext>();
        await using var transaction = await CeremonyTransaction.OpenApiReadAsync(database, services.GetRequiredService<NativeOperation>(), identity.Subject, token);
        await services.GetRequiredService<ProtocolStorageSelection>().SelectApiReadAsync(transaction, database, identity.Subject);
        var access = await ApiCurrentAccess.ReadAsync(services, database, transaction, identity, profile.Callback, clock);
        if (access.Status != StatusCodes.Status200OK)
        {
            return (access.Status, null, access.SourceExpiry);
        }
        var expiry = DateTimeOffset.FromUnixTimeSeconds(access.SourceExpiry);
        var page = await ApplicationListReader.ReadAsync(database, query, identity.ExpiresAt < expiry ? identity.ExpiresAt : expiry, transaction.Token);
        transaction.RequireApiRead(database, identity.Subject);
        return (StatusCodes.Status200OK, page, access.SourceExpiry);
    }
}
