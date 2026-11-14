using System.Security.Claims;
using IdentityPlatform.AuthServer.Features.Protocol.ManagedApplications;
using IdentityPlatform.Infrastructure.Features.Applications;
using IdentityPlatform.Infrastructure.Features.Identity.ManagedApplications;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;

namespace IdentityPlatform.AuthServer.Features.Protocol.UserInfo;

/// <summary>Reconstructs current managed bearer authority under governance and account locks without creating or consuming credentials.</summary>
internal sealed class UserInfoAuthority(ManagedRegistration registration, ManagedLoginInteraction row, ManagedHumanSnapshot source,
    ManagedEmailProjection? email, DateTimeOffset expires)
{
    internal string Subject => row.UserId!;
    internal ManagedEmailProjection? Email => email;
    internal DateTimeOffset Expires => expires;

    /// <summary>Resolves the unique consent interaction from native authorization identity, never a public selector or copied email claim.</summary>
    internal static async ValueTask<UserInfoAuthority?> ReadAsync(ProtocolStorageScope scope, ClaimsPrincipal principal, CancellationToken token)
    {
        if (!Candidate(principal))
        {
            return null;
        }
        await ManagedRegistration.LockAsync(scope, token);
        var client = principal.GetPresenters()[0];
        var registration = await ManagedRegistration.ReadAsync(scope, client, token);
        var authorization = principal.GetAuthorizationId();
        var row = await scope.Owner.RunAsync(current => new ValueTask<ManagedLoginInteraction?>(scope.Owner.Database.Set<ManagedLoginInteraction>()
            .AsNoTracking().SingleOrDefaultAsync(value => value.AuthorizationId == authorization, current)), token);
        if (registration is null || row is null || !Bound(principal, registration, row) ||
            !await ManagedScopePolicy.TryVerifyAsync(scope, client, ManagedScopeBinding.Read(row), token))
        {
            return null;
        }
        var expiration = await NativeAsync(scope, principal, registration, token);
        if (expiration is null || await ManagedIssuanceSource.TryOpenAsync(scope, row, observe: false, token) is not { } source)
        {
            return null;
        }
        var deadline = Math.Min(expiration.Value.ToUnixTimeSeconds(), Math.Min(row.ExpiresAt, registration.ExpiresAt));
        var result = new UserInfoAuthority(registration, row, source.Snapshot(row), source.Email(row), DateTimeOffset.FromUnixTimeSeconds(deadline));
        return await result.VerifyAsync(scope, principal, token) ? result : null;
    }

    /// <summary>Requires an authenticated managed access token with a single exact presenter and no fixed-profile or identity disclosure claims.</summary>
    private static bool Candidate(ClaimsPrincipal principal) => principal.Identities.Count() == 1 &&
        principal.Identity is ClaimsIdentity { IsAuthenticated: true, Actor: null } && principal.HasTokenType(OpenIddictConstants.TokenTypeIdentifiers.AccessToken) &&
        principal.GetPresenters() is [var client] && ManagedDemand.Candidate(client) &&
        principal.GetClaim(OpenIddictConstants.Claims.ClientId) == client && !string.IsNullOrEmpty(principal.GetAuthorizationId()) &&
        !string.IsNullOrEmpty(principal.GetTokenId()) && !string.IsNullOrEmpty(principal.GetClaim(OpenIddictConstants.Claims.Subject)) &&
        !principal.HasClaim(claim => claim.Type is OpenIddictConstants.Claims.Email or OpenIddictConstants.Claims.EmailVerified or OpenIddictConstants.Claims.Role);

    /// <summary>Binds exact current application, subject, consumed consent, granted scopes and the original resource without substituting catalogue authority.</summary>
    private static bool Bound(ClaimsPrincipal principal, ManagedRegistration registration, ManagedLoginInteraction row)
    {
        var decision = ManagedScopeBinding.Read(row);
        return row.State == ManagedLoginState.Consumed && row.AuthorizationId == principal.GetAuthorizationId() &&
            row.UserId == principal.GetClaim(OpenIddictConstants.Claims.Subject) && row.ApplicationId == registration.ApplicationId &&
            row.ApplicationRevision == registration.Revision && decision.Requested.SequenceEqual(decision.Granted, StringComparer.Ordinal) &&
            principal.GetScopes().SequenceEqual(decision.Granted, StringComparer.Ordinal) && principal.GetAudiences().SequenceEqual(decision.Resources, StringComparer.Ordinal);
    }

    /// <summary>Reloads both native token and authorization status after lock acquisition; cryptographic validation alone cannot establish current grant state.</summary>
    private static async ValueTask<DateTimeOffset?> NativeAsync(ProtocolStorageScope scope, ClaimsPrincipal principal, ManagedRegistration registration, CancellationToken token)
    {
        var expiration = await AdministratorNativeToken.ExpirationAsync(scope, principal, OpenIddictConstants.TokenTypeIdentifiers.AccessToken, registration.ApplicationId, token);
        var tokens = scope.Services.GetRequiredService<IOpenIddictTokenManager>();
        var entry = await tokens.FindByIdAsync(principal.GetTokenId()!, token);
        if (expiration is null || entry is null || !await tokens.HasStatusAsync(entry, OpenIddictConstants.Statuses.Valid, token))
        {
            return null;
        }
        var authorizations = scope.Services.GetRequiredService<IOpenIddictAuthorizationManager>();
        var authorization = await authorizations.FindByIdAsync(principal.GetAuthorizationId()!, token);
        return authorization is not null && await ProtocolStorageRows.ReloadAsync(scope, authorization, token) &&
            await authorizations.GetIdAsync(authorization, token) == principal.GetAuthorizationId() &&
            await authorizations.GetApplicationIdAsync(authorization, token) == registration.ApplicationId &&
            await authorizations.GetSubjectAsync(authorization, token) == principal.GetClaim(OpenIddictConstants.Claims.Subject) &&
            await authorizations.HasStatusAsync(authorization, OpenIddictConstants.Statuses.Valid, token) &&
            await authorizations.HasTypeAsync(authorization, OpenIddictConstants.AuthorizationTypes.AdHoc, token) &&
            (await authorizations.GetScopesAsync(authorization, token)).SequenceEqual(principal.GetScopes(), StringComparer.Ordinal)
            ? UserInfoLifetime.Bound(principal, expiration.Value, scope.Owner.Clock.GetUtcNow()) : null;
    }

    /// <summary>Rechecks independent persisted account/ceremony/consent state and fresh native policy after formatter callbacks, before committing the read phase.</summary>
    internal async ValueTask<bool> VerifyAsync(ProtocolStorageScope scope, ClaimsPrincipal principal, CancellationToken token)
    {
        await scope.Owner.RunAsync(current => new ValueTask(source.VerifyAsync((PlatformDbContext)scope.Owner.Database, scope.Owner.Clock, current)), token);
        return Expires > scope.Owner.Clock.GetUtcNow() && Candidate(principal) && Bound(principal, registration, row) &&
            registration.Matches(await ManagedRegistration.ReadAsync(scope, registration.ClientId, token)) &&
            await ManagedScopePolicy.TryVerifyAsync(scope, registration.ClientId, ManagedScopeBinding.Read(row), token) &&
            await NativeAsync(scope, principal, registration, token) is { } current && current >= Expires;
    }
}
