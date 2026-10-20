using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;

/// <summary>Reads persisted outcomes through separate native manager scopes after the original HTTP flow has fully cleaned up.</summary>
internal static class NativeAccountPersistence
{
    /// <summary>Reads the committed profile watermark independently of the clock intervention and issuance's tracked entities.</summary>
    internal static Task<long> ObservedAtAsync(AdministratorProtocolScenario scenario) => ReadAsync(scenario, (scope, token) =>
        scope.Owner.RunAsync(current => new ValueTask<long>(scope.Owner.Database.Set<AdministratorProtocolProfile>().AsNoTracking()
            .Select(row => row.ObservedAt).SingleAsync(current)), token));

    /// <summary>Counts actual retained credential rows without inspecting or serializing their protected payloads.</summary>
    internal static Task<(long Tokens, long Authorizations)> CountAsync(AdministratorProtocolScenario scenario) => ReadAsync(scenario, async (scope, token) =>
        (await scope.Services.GetRequiredService<IOpenIddictTokenManager>().CountAsync(token),
            await scope.Services.GetRequiredService<IOpenIddictAuthorizationManager>().CountAsync(token)));

    /// <summary>Counts committed native access-token entries independently of a deliberately lost token response.</summary>
    internal static Task<int> AccessTokensAsync(AdministratorProtocolScenario scenario) => ReadAsync(scenario, (scope, token) =>
        scope.Owner.RunAsync(current => new ValueTask<int>(scope.Owner.Database.Set<OpenIddictEntityFrameworkCoreToken>()
            .AsNoTracking().CountAsync(row => row.Type == OpenIddictConstants.TokenTypeIdentifiers.AccessToken &&
                row.Status == OpenIddictConstants.Statuses.Valid, current)), token));

    /// <summary>Resolves the actual native reference identifier before later redemption so rollback assertions do not depend on HTTP success.</summary>
    internal static Task<string> ReferenceIdAsync(AdministratorProtocolScenario scenario, string reference) => ReadAsync(scenario, async (scope, token) =>
    {
        var manager = scope.Services.GetRequiredService<IOpenIddictTokenManager>();
        var row = await manager.FindByReferenceIdAsync(reference[OpenIddictConstants.RequestUris.Prefixes.Generic.Length..], token);
        Assert.NotNull(row);
        return (await manager.GetIdAsync(row, token))!;
    });

    /// <summary>Verifies early native consumption remains durable despite rollback of the later issuance epoch.</summary>
    internal static async Task<bool> IsRedeemedAsync(AdministratorProtocolScenario scenario, string identifier) =>
        await StatusAsync(scenario, identifier) == OpenIddictConstants.Statuses.Redeemed;

    /// <summary>Reads the actual durable native status so pre-sign-in denials cannot be mistaken for redemption.</summary>
    internal static Task<string?> StatusAsync(AdministratorProtocolScenario scenario, string identifier) => ReadAsync(scenario, async (scope, token) =>
    {
        var manager = scope.Services.GetRequiredService<IOpenIddictTokenManager>();
        var row = await manager.FindByIdAsync(identifier, token);
        return row is null ? null : await manager.GetStatusAsync(row, token);
    });

    /// <summary>Starts a fresh original read request for each independent persisted observation.</summary>
    private static Task<T> ReadAsync<T>(AdministratorProtocolScenario scenario, Func<ProtocolStorageScope, CancellationToken, ValueTask<T>> read) =>
        new ProtocolStorageRequest(scenario.Account.Journey.Clock, TestContext.Current.CancellationToken)
            .RunAsync(scenario.Services.GetRequiredService<IServiceScopeFactory>(), (scope, token) =>
            {
                scope.Services.GetRequiredService<ProtocolStorageSelection>().Select(ProtocolStorageMode.Owned);
                return read(scope, token);
            });
}
