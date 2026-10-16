using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Persistence.Tests.Features.ProtocolStorage;

/// <summary>Exercises native post-query filtering and independent observations after partial creation rolls back.</summary>
internal static class NativeOwnershipQueries
{
    /// <summary>All four entity families must disappear after a transaction creates them and exits without commit.</summary>
    internal static async Task RequireEmptyAsync(IServiceProvider root, CancellationToken token)
    {
        await using var scope = await NativeOwnershipConformance.OpenAsync(root, token);
        Assert.Equal(0, await scope.Services.GetRequiredService<IOpenIddictApplicationManager>().CountAsync(token));
        Assert.Equal(0, await scope.Services.GetRequiredService<IOpenIddictAuthorizationManager>().CountAsync(token));
        Assert.Equal(0, await scope.Services.GetRequiredService<IOpenIddictScopeManager>().CountAsync(token));
        Assert.Equal(0, await scope.Services.GetRequiredService<IOpenIddictTokenManager>().CountAsync(token));
    }

    /// <summary>Pure URI/resource/scopes getters run inside native enumeration while guarded relationship getters run between yields.</summary>
    internal static async Task VerifyFiltersAsync(IServiceProvider root, CancellationToken token)
    {
        await using var scope = await NativeOwnershipConformance.OpenAsync(root, token);
        var applications = scope.Services.GetRequiredService<IOpenIddictApplicationManager>();
        await RequireOneAsync(applications.FindByRedirectUriAsync(NativeOwnershipConformance.Redirect, token), token);
        await RequireOneAsync(applications.FindByPostLogoutRedirectUriAsync(NativeOwnershipConformance.Logout, token), token);
        var scopes = scope.Services.GetRequiredService<IOpenIddictScopeManager>();
        await RequireOneAsync(scopes.FindByResourceAsync(NativeOwnershipConformance.ResourceName, token), token);
        var authorizations = scope.Services.GetRequiredService<IOpenIddictAuthorizationManager>();
        await RequireOneAsync(authorizations.FindAsync(NativeOwnershipConformance.Subject, null,
            OpenIddictConstants.Statuses.Valid, OpenIddictConstants.AuthorizationTypes.Permanent, [NativeOwnershipConformance.ScopeName], token), token);
        var tokens = scope.Services.GetRequiredService<IOpenIddictTokenManager>();
        var authorization = await SingleAsync(authorizations.FindBySubjectAsync(NativeOwnershipConformance.Subject, token), token);
        await RequireOneAsync(tokens.FindByAuthorizationIdAsync((await authorizations.GetIdAsync(authorization, token))!, token), token);
        await RequireGenericListAsync(scope, token);
    }

    /// <summary>The generic deferred native query retains ownership through actual enumeration and disposal.</summary>
    private static async Task RequireGenericListAsync(ProtocolStorageScope scope, CancellationToken token)
    {
        var store = scope.Services.GetRequiredService<IOpenIddictTokenStore<OpenIddictEntityFrameworkCoreToken>>();
        List<string?> rows = [];
        await foreach (var row in store.ListAsync((query, subject) => query.Where(row => row.Subject == subject).Select(row => row.Subject), NativeOwnershipConformance.Subject, token))
        {
            rows.Add(row);
        }
        Assert.Equal(NativeOwnershipConformance.Subject, Assert.Single(rows));
    }

    /// <summary>Fully enumerates an actual manager source and requires a single matching native entity.</summary>
    private static async Task RequireOneAsync(IAsyncEnumerable<object> source, CancellationToken token) =>
        Assert.NotNull(await SingleAsync(source, token));

    /// <summary>Collects manager results without changing native filtering or store-side reader lifetime.</summary>
    private static async Task<object> SingleAsync(IAsyncEnumerable<object> source, CancellationToken token)
    {
        List<object> rows = [];
        await foreach (var row in source.WithCancellation(token))
        {
            rows.Add(row);
        }
        return Assert.Single(rows);
    }
}
