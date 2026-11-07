using System.Data.Common;
using IdentityPlatform.AuthServer.Features.Pilot;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace IdentityPlatform.AuthServer.Features.Protocol.PilotReplay;

/// <summary>Repeats native chain cleanup when a revoked pilot code is replayed after an overlapping winner stored tokens; native validation still rejects the code.</summary>
internal sealed class PilotReplayCleanup(PlatformDbContext database, IOpenIddictApplicationManager applications,
    IOpenIddictAuthorizationManager authorizations, IOpenIddictTokenManager tokens, ILogger<PilotReplayCleanup> logger)
    : IOpenIddictServerHandler<OpenIddictServerEvents.ValidateTokenContext>
{
    private static readonly Action<ILogger, Exception?> CleanupFailed = LoggerMessage.Define(LogLevel.Warning,
        new EventId(7310), "Pilot replay chain cleanup did not complete.");

    /// <summary>Never changes validation success or rejection; current native persisted bindings are required before idempotent cleanup.</summary>
    public async ValueTask HandleAsync(OpenIddictServerEvents.ValidateTokenContext context)
    {
        var principal = context.Principal;
        if (context.EndpointType != OpenIddictServerEndpointType.Token || context.Transaction.Request is not { } request ||
            !request.IsAuthorizationCodeGrantType() || request.ClientId != PilotProfile.ClientId || principal is null ||
            !principal.HasTokenType(OpenIddictConstants.TokenTypeIdentifiers.Private.AuthorizationCode) ||
            principal.GetPresenters() is not [PilotProfile.ClientId] || string.IsNullOrEmpty(context.TokenId) ||
            string.IsNullOrEmpty(context.AuthorizationId) || context.TokenId != principal.GetTokenId() ||
            context.AuthorizationId != principal.GetAuthorizationId() || string.IsNullOrEmpty(principal.GetClaim(OpenIddictConstants.Claims.Subject)))
        {
            return;
        }
        if (!await IsRevokedPilotCodeAsync(context))
        {
            return;
        }
        try
        {
            await tokens.RevokeByAuthorizationIdAsync(context.AuthorizationId, context.CancellationToken);
        }
        catch (Exception error) when (error is DbException or DbUpdateException or InvalidOperationException or OperationCanceledException)
        {
            // Match native best-effort cleanup semantics without logging token, authorization or exception payloads.
            CleanupFailed(logger, null);
        }
    }

    /// <summary>Binds the exact native code, authorization, subject and application to the persisted pilot manifest before any write.</summary>
    private async Task<bool> IsRevokedPilotCodeAsync(OpenIddictServerEvents.ValidateTokenContext context)
    {
        var cancellation = context.CancellationToken;
        var code = await tokens.FindByIdAsync(context.TokenId!, cancellation);
        if (code is null || await tokens.GetIdAsync(code, cancellation) != context.TokenId ||
            !await tokens.HasTypeAsync(code, OpenIddictConstants.TokenTypeIdentifiers.Private.AuthorizationCode, cancellation) ||
            !await tokens.HasStatusAsync(code, OpenIddictConstants.Statuses.Revoked, cancellation) ||
            await tokens.GetAuthorizationIdAsync(code, cancellation) != context.AuthorizationId ||
            await tokens.GetSubjectAsync(code, cancellation) != context.Principal!.GetClaim(OpenIddictConstants.Claims.Subject))
        {
            return false;
        }
        var application = await applications.FindByClientIdAsync(PilotProfile.ClientId, cancellation);
        var id = application is null ? null : await applications.GetIdAsync(application, cancellation);
        if (string.IsNullOrEmpty(id) || await applications.GetClientIdAsync(application!, cancellation) != PilotProfile.ClientId ||
            await tokens.GetApplicationIdAsync(code, cancellation) != id ||
            !await database.Set<PilotManifest>().AsNoTracking().AnyAsync(row => row.Id == PilotProfile.Id && row.ApplicationId == id, cancellation))
        {
            return false;
        }
        var authorization = await authorizations.FindByIdAsync(context.AuthorizationId!, cancellation);
        return authorization is not null && await authorizations.GetIdAsync(authorization, cancellation) == context.AuthorizationId &&
            await authorizations.GetApplicationIdAsync(authorization, cancellation) == id &&
            await authorizations.GetSubjectAsync(authorization, cancellation) == context.Principal!.GetClaim(OpenIddictConstants.Claims.Subject);
    }
}
