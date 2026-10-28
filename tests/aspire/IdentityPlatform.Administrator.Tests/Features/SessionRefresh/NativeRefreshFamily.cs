using System.Security.Claims;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Administrator.Tests.Features.SessionRefresh;

/// <summary>Retains only native row selectors and prevalidated synthetic stage principals; never represents a browser credential.</summary>
internal sealed record NativeRefreshFamily(string Authorization, ClaimsPrincipal Predecessor, ClaimsPrincipal Successor);

/// <summary>Creates and observes real native manager state under explicit owned transactions on either provider.</summary>
internal static class NativeRefreshRows
{
    private const string Subject = "refresh-characterization-subject";
    private const string Scope = "identity.admin";
    private const int OriginalHours = 8;
    private const int LinkedTokenCount = 2;

    /// <summary>Commits one native authorization and two tokens to characterize predecessor/successor effects without issuing a token string.</summary>
    internal static Task<NativeRefreshFamily> CreateAsync(NativeRefreshFixture fixture, bool redeemed = false, bool redemptionDate = true) =>
        fixture.CommitAsync(async (scope, token) =>
        {
            var applications = scope.Services.GetRequiredService<IOpenIddictApplicationManager>();
            var application = await applications.FindByClientIdAsync(NativeRefreshStages.Client, token) ??
                await applications.CreateAsync(new OpenIddictApplicationDescriptor
                {
                    ClientId = NativeRefreshStages.Client,
                    ClientType = OpenIddictConstants.ClientTypes.Public
                }, token);
            var applicationId = await applications.GetIdAsync(application, token);
            var authorizations = scope.Services.GetRequiredService<IOpenIddictAuthorizationManager>();
            var authorization = await authorizations.CreateAsync(new OpenIddictAuthorizationDescriptor
            {
                ApplicationId = applicationId,
                Subject = Subject,
                Status = OpenIddictConstants.Statuses.Valid,
                Type = OpenIddictConstants.AuthorizationTypes.AdHoc,
                Scopes = { Scope, OpenIddictConstants.Scopes.OfflineAccess }
            }, token);
            var id = (await authorizations.GetIdAsync(authorization, token))!;
            var predecessor = await CreateTokenAsync(scope, applicationId!, id, fixture.Clock.GetUtcNow(), redeemed, redemptionDate, token);
            var successor = await CreateTokenAsync(scope, applicationId!, id, fixture.Clock.GetUtcNow(), false, false, token);
            return new NativeRefreshFamily(id, predecessor, successor);
        });

    /// <summary>Uses native token manager validation and persisted dates; the returned principal is an explicit post-crypto seam.</summary>
    private static async ValueTask<ClaimsPrincipal> CreateTokenAsync(ProtocolStorageScope scope, string application, string authorization,
        DateTimeOffset now, bool redeemed, bool redemptionDate, CancellationToken token)
    {
        var manager = scope.Services.GetRequiredService<IOpenIddictTokenManager>();
        var row = await manager.CreateAsync(new OpenIddictTokenDescriptor
        {
            ApplicationId = application,
            AuthorizationId = authorization,
            Subject = Subject,
            CreationDate = now,
            ExpirationDate = now.AddHours(OriginalHours),
            RedemptionDate = redeemed && redemptionDate ? now : null,
            Status = redeemed ? OpenIddictConstants.Statuses.Redeemed : OpenIddictConstants.Statuses.Valid,
            Type = OpenIddictConstants.TokenTypeHints.RefreshToken
        }, token);
        return new ClaimsPrincipal(new ClaimsIdentity()).SetTokenType(OpenIddictConstants.TokenTypeIdentifiers.RefreshToken)
            .SetTokenId(await manager.GetIdAsync(row, token)).SetAuthorizationId(authorization).SetPresenters(NativeRefreshStages.Client)
            .SetClaim(OpenIddictConstants.Claims.Subject, Subject).SetScopes(Scope, OpenIddictConstants.Scopes.OfflineAccess)
            .SetCreationDate(now).SetExpirationDate(now.AddHours(OriginalHours));
    }

    /// <summary>Reads exact committed statuses through fresh manager instances, independent of a mutation's tracked entities.</summary>
    internal static Task<(string? Authorization, string? Predecessor, string? Successor)> StatusAsync(NativeRefreshFixture fixture, NativeRefreshFamily family) =>
        fixture.RunAsync(async (scope, token) =>
        {
            var authorizations = scope.Services.GetRequiredService<IOpenIddictAuthorizationManager>();
            var manager = scope.Services.GetRequiredService<IOpenIddictTokenManager>();
            var authorization = await authorizations.FindByIdAsync(family.Authorization, token);
            var previous = await manager.FindByIdAsync(family.Predecessor.GetTokenId()!, token);
            var next = await manager.FindByIdAsync(family.Successor.GetTokenId()!, token);
            Assert.NotNull(authorization);
            Assert.NotNull(previous);
            Assert.NotNull(next);
            return (await authorizations.GetStatusAsync(authorization, token), await manager.GetStatusAsync(previous, token),
                await manager.GetStatusAsync(next, token));
        });

    /// <summary>Demonstrates atomic manager-owned family denial; authentication/target selection is deliberately outside this primitive.</summary>
    internal static async ValueTask<bool> RevokeFamilyAsync(ProtocolStorageScope scope, NativeRefreshFamily family, CancellationToken token)
    {
        var manager = scope.Services.GetRequiredService<IOpenIddictAuthorizationManager>();
        var authorization = await manager.FindByIdAsync(family.Authorization, token);
        Assert.NotNull(authorization);
        Assert.True(await manager.TryRevokeAsync(authorization, token));
        Assert.Equal(LinkedTokenCount, await scope.Services.GetRequiredService<IOpenIddictTokenManager>().RevokeByAuthorizationIdAsync(family.Authorization, token));
        return true;
    }
}
