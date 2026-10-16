using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Persistence.Tests.Features.ProtocolStorage;

/// <summary>Exercises the same ownership and native manager behaviors independently of provider and store selection.</summary>
internal static class NativeOwnershipConformance
{
    internal const string Client = "ownership-client";
    internal const string ScopeName = "ownership-scope";
    internal const string Redirect = "https://ownership.example.test/callback";
    internal const string Logout = "https://ownership.example.test/logout";
    internal const string ResourceName = "ownership-resource";
    internal const string Subject = "ownership-subject";

    /// <summary>Combines durability, relationship enumeration, nested native helper rollback and terminal cancellation controls.</summary>
    internal static async Task VerifyAsync(ServiceProvider root, CancellationToken token)
    {
        await SeedAsync(root, false, token);
        await NativeOwnershipQueries.RequireEmptyAsync(root, token);
        await SeedAsync(root, true, token);
        await NativeOwnershipQueries.VerifyFiltersAsync(root, token);
        await RequireStateAsync(root, true, token);
        await RollbackDeletesAsync(root, false, token);
        await RequireStateAsync(root, true, token);
        await RollbackDeletesAsync(root, true, token);
        await RequireStateAsync(root, true, token);
        await RollbackRevocationAsync(root, token);
        await RequireStateAsync(root, true, token);
        await NativeOwnershipFailures.VerifyAsync(root, token);
        await RequireStateAsync(root, true, token);
        await CommitDeletionAsync(root, token);
        await RequireStateAsync(root, false, token);
    }

    /// <summary>Persists all four native entity families only through installed managers and one explicit owner transaction.</summary>
    private static async Task SeedAsync(IServiceProvider root, bool commit, CancellationToken token)
    {
        await using var scope = await OpenAsync(root, token);
        await using var transaction = await ProtocolStorageTransaction.OpenAsync(scope.Owner);
        var applications = scope.Services.GetRequiredService<IOpenIddictApplicationManager>();
        var application = await applications.CreateAsync(new OpenIddictApplicationDescriptor
        {
            ClientId = Client,
            ClientType = OpenIddictConstants.ClientTypes.Public,
            RedirectUris = { new Uri(Redirect) },
            PostLogoutRedirectUris = { new Uri(Logout) }
        }, token);
        var scopes = scope.Services.GetRequiredService<IOpenIddictScopeManager>();
        await scopes.CreateAsync(new OpenIddictScopeDescriptor { Name = ScopeName, Resources = { ResourceName } }, token);
        var authorizations = scope.Services.GetRequiredService<IOpenIddictAuthorizationManager>();
        var authorization = await authorizations.CreateAsync(new OpenIddictAuthorizationDescriptor
        {
            ApplicationId = await applications.GetIdAsync(application, token),
            Subject = Subject,
            Status = OpenIddictConstants.Statuses.Valid,
            Type = OpenIddictConstants.AuthorizationTypes.Permanent,
            Scopes = { ScopeName }
        }, token);
        await scope.Services.GetRequiredService<IOpenIddictTokenManager>().CreateAsync(new OpenIddictTokenDescriptor
        {
            ApplicationId = await applications.GetIdAsync(application, token),
            AuthorizationId = await authorizations.GetIdAsync(authorization, token),
            Subject = Subject,
            Status = OpenIddictConstants.Statuses.Valid,
            Type = OpenIddictConstants.TokenTypeHints.AccessToken
        }, token);
        Assert.Equal(1, await scope.Services.GetRequiredService<IOpenIddictTokenManager>().CountAsync(token));
        if (commit)
        {
            await transaction.CommitAsync(token);
        }
    }

    /// <summary>Reads committed state in a new native context and exercises actual manager relationship filtering between yields.</summary>
    internal static async Task RequireStateAsync(IServiceProvider root, bool exists, CancellationToken token)
    {
        await using var scope = await OpenAsync(root, token);
        var applications = scope.Services.GetRequiredService<IOpenIddictApplicationManager>();
        var application = await applications.FindByClientIdAsync(Client, token);
        if (!exists)
        {
            Assert.Null(application);
            return;
        }
        Assert.NotNull(application);
        var id = await applications.GetIdAsync(application, token);
        var tokens = scope.Services.GetRequiredService<IOpenIddictTokenManager>();
        List<object> rows = [];
        await foreach (var row in tokens.FindByApplicationIdAsync(id!, token))
        {
            rows.Add(row);
        }
        var native = Assert.IsType<OpenIddictEntityFrameworkCoreToken>(Assert.Single(rows));
        Assert.Equal(OpenIddictConstants.Statuses.Valid, await tokens.GetStatusAsync(native, token));
        var authorizations = scope.Services.GetRequiredService<IOpenIddictAuthorizationManager>();
        Assert.NotNull(await authorizations.FindByIdAsync((await tokens.GetAuthorizationIdAsync(native, token))!, token));
        var scopes = scope.Services.GetRequiredService<IOpenIddictScopeManager>();
        Assert.NotNull(await scopes.FindByNameAsync(ScopeName, token));
        var store = scope.Services.GetRequiredService<IOpenIddictTokenStore<OpenIddictEntityFrameworkCoreToken>>();
        Assert.Equal(1, await store.CountAsync(query => query.Where(row => row.Subject == Subject), token));
        Assert.Equal(Subject, await store.GetAsync((query, subject) => query.Where(row => row.Subject == subject).Select(row => row.Subject), Subject, token));
    }

    /// <summary>Native application/authorization Delete must join the owned transaction despite its nested helper catching Begin errors.</summary>
    private static async Task RollbackDeletesAsync(IServiceProvider root, bool authorization, CancellationToken token)
    {
        await using var scope = await OpenAsync(root, token);
        await using (var transaction = await ProtocolStorageTransaction.OpenAsync(scope.Owner))
        {
            var original = scope.Owner.Database.Database.CurrentTransaction;
            if (authorization)
            {
                var manager = scope.Services.GetRequiredService<IOpenIddictAuthorizationManager>();
                List<object> rows = [];
                await foreach (var row in manager.FindBySubjectAsync(Subject, token))
                {
                    rows.Add(row);
                }
                var target = Assert.Single(rows);
                var identifier = await manager.GetIdAsync(target, token);
                await manager.DeleteAsync(target, token);
                Assert.Null(await manager.FindByIdAsync(identifier!, token));
            }
            else
            {
                var manager = scope.Services.GetRequiredService<IOpenIddictApplicationManager>();
                await manager.DeleteAsync((await manager.FindByClientIdAsync(Client, token))!, token);
                Assert.Null(await manager.FindByClientIdAsync(Client, token));
            }
            Assert.Equal(0, await scope.Services.GetRequiredService<IOpenIddictTokenManager>().CountAsync(token));
            Assert.Same(original, scope.Owner.Database.Database.CurrentTransaction);
        }
        Assert.Throws<InvalidOperationException>(() => scope.Owner.Capture());
    }

    /// <summary>Bulk revocation bypasses SaveChanges but still remains inside the same rollback boundary.</summary>
    private static async Task RollbackRevocationAsync(IServiceProvider root, CancellationToken token)
    {
        await using var scope = await OpenAsync(root, token);
        await using var transaction = await ProtocolStorageTransaction.OpenAsync(scope.Owner);
        var tokens = scope.Services.GetRequiredService<IOpenIddictTokenManager>();
        Assert.Equal(1, await tokens.RevokeBySubjectAsync(Subject, token));
    }

    /// <summary>A clean application deletion becomes durable only at the owner commit boundary.</summary>
    private static async Task CommitDeletionAsync(IServiceProvider root, CancellationToken token)
    {
        await using var scope = await OpenAsync(root, token);
        await using var transaction = await ProtocolStorageTransaction.OpenAsync(scope.Owner);
        var manager = scope.Services.GetRequiredService<IOpenIddictApplicationManager>();
        await manager.DeleteAsync((await manager.FindByClientIdAsync(Client, token))!, token);
        await transaction.CommitAsync(token);
    }

    /// <summary>Creates a fresh owned child graph for each independent durable observation or transaction phase.</summary>
    internal static Task<ProtocolStorageScope> OpenAsync(IServiceProvider root, CancellationToken token) =>
        ProtocolStorageScope.OpenAsync(root.GetRequiredService<IServiceScopeFactory>(), token);
}
