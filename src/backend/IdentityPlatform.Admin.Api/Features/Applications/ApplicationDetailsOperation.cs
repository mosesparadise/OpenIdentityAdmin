using IdentityPlatform.Admin.Api.Features.Authentication;
using IdentityPlatform.Core.Features.Applications;
using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Persistence;

namespace IdentityPlatform.Admin.Api.Features.Applications;

/// <summary>Owns a current-authority details read through actual rollback and scope disposal; no registration or authentication state is changed.</summary>
internal static class ApplicationDetailsOperation
{
    /// <summary>Retains the list's rollback-only ownership and current applications.read permission for one exact application.</summary>
    internal static async Task<(ApplicationDetails Details, string? Version)> ReadAsync(IServiceScopeFactory factory, ApiIdentity identity,
        string id, ApiResourceProfile profile, TimeProvider clock, CancellationToken token)
    {
        var result = await ReadOwnedAsync(factory, identity, profile, clock,
            (database, expiry, current) => ApplicationDetailsReader.ReadAsync(database, id, expiry, current), token);
        return result.Details is { } details ? (details, result.Version) : throw new ApplicationHttpRejected(StatusCodes.Status404NotFound);
    }

    /// <summary>Shares current application read authority and actual rollback/disposal with finite application subresources; it grants no commit authority.</summary>
    internal static async Task<T> ReadOwnedAsync<T>(IServiceScopeFactory factory, ApiIdentity identity, ApiResourceProfile profile,
        TimeProvider clock, Func<PlatformDbContext, DateTimeOffset, CancellationToken, Task<T>> reader, CancellationToken token)
    {
        await using var scope = factory.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var database = services.GetRequiredService<PlatformDbContext>();
        await using var transaction = await CeremonyTransaction.OpenApiReadAsync(database, services.GetRequiredService<NativeOperation>(), identity.Subject, token);
        await services.GetRequiredService<ProtocolStorageSelection>().SelectApiReadAsync(transaction, database, identity.Subject);
        var access = await ApiCurrentAccess.ReadAsync(services, database, transaction, identity, profile.Callback, clock);
        if (access.Status != StatusCodes.Status200OK)
        {
            throw new ApplicationHttpRejected(access.Status);
        }
        var expiry = DateTimeOffset.FromUnixTimeSeconds(access.SourceExpiry);
        var result = await reader(database, identity.ExpiresAt < expiry ? identity.ExpiresAt : expiry, transaction.Token);
        transaction.RequireApiRead(database, identity.Subject);
        return result;
    }
}
