using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;

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

    /// <summary>Resolves the actual native reference identifier before later redemption so rollback assertions do not depend on HTTP success.</summary>
    internal static Task<string> ReferenceIdAsync(AdministratorProtocolScenario scenario, string reference) => ReadAsync(scenario, async (scope, token) =>
    {
        var manager = scope.Services.GetRequiredService<IOpenIddictTokenManager>();
        var row = await manager.FindByReferenceIdAsync(reference[OpenIddictConstants.RequestUris.Prefixes.Generic.Length..], token);
        Assert.NotNull(row);
        return (await manager.GetIdAsync(row, token))!;
    });

    /// <summary>Verifies early native consumption remains durable despite rollback of the later issuance epoch.</summary>
    internal static Task<bool> IsRedeemedAsync(AdministratorProtocolScenario scenario, string identifier) => ReadAsync(scenario, async (scope, token) =>
    {
        var manager = scope.Services.GetRequiredService<IOpenIddictTokenManager>();
        var row = await manager.FindByIdAsync(identifier, token);
        return row is not null && await manager.GetStatusAsync(row, token) == OpenIddictConstants.Statuses.Redeemed;
    });

    /// <summary>Starts a fresh original read request for each independent persisted observation.</summary>
    private static Task<T> ReadAsync<T>(AdministratorProtocolScenario scenario, Func<ProtocolStorageScope, CancellationToken, ValueTask<T>> read) =>
        new ProtocolStorageRequest(scenario.Account.Journey.Clock, TestContext.Current.CancellationToken)
            .RunAsync(scenario.Services.GetRequiredService<IServiceScopeFactory>(), read);
}
