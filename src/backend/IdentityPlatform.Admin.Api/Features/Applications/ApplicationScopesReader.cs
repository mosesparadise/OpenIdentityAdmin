using IdentityPlatform.Core.Features.Applications;
using IdentityPlatform.Infrastructure.Persistence;

namespace IdentityPlatform.Admin.Api.Features.Applications;

/// <summary>Reads only managed application scope names under the existing current-authority rollback-only transaction.</summary>
internal static class ApplicationScopesReader
{
    /// <summary>Reuses exact profile validation and publishes assigned names even when their catalogue policy is disabled and needs removal.</summary>
    internal static async Task<(ApplicationScopes Scopes, string Version)> ReadAsync(PlatformDbContext database, string id, DateTimeOffset expiry, CancellationToken token)
    {
        var (details, version, names) = await ApplicationDetailsReader.ReadStateAsync(database, id, expiry, token);
        if (details is null)
        {
            throw new ApplicationHttpRejected(StatusCodes.Status404NotFound);
        }
        if (!details.Editable || version is null)
        {
            throw new ApplicationHttpRejected(StatusCodes.Status409Conflict);
        }
        var output = new ApplicationScopes(id, names!, details.ExpiresAt);
        return ApplicationScopeRules.IsValid(output, id) ? (output, version) : throw new InvalidDataException();
    }
}
