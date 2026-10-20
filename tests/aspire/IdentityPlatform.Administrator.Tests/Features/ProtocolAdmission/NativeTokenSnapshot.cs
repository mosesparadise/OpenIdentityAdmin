using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;

/// <summary>Retains one exact synthetic native row privately to demonstrate resurrection consequences, not provider backup restoration.</summary>
internal sealed class NativeTokenSnapshot(string identifier, PropertyValues original, bool clientState)
{
    internal string Identifier => identifier;

    /// <summary>Captures an actual Valid code or the fixture's sole Valid callback state, preserving every scalar field including payload and concurrency token.</summary>
    internal static Task<NativeTokenSnapshot> CaptureAsync(IServiceProvider root, bool clientState, string? identifier = null) =>
        ReadAsync(root, (scope, token) => scope.Owner.RunAsync(async current =>
        {
            var rows = scope.Owner.Database.Set<OpenIddictEntityFrameworkCoreToken>();
            var row = identifier is null ? await rows.SingleAsync(current) : await rows.SingleAsync(value => value.Id == identifier, current);
            Assert.Equal(OpenIddictConstants.Statuses.Valid, row.Status);
            Assert.Equal(clientState ? OpenIddictConstants.TokenTypeIdentifiers.Private.StateToken :
                OpenIddictConstants.TokenTypeIdentifiers.Private.AuthorizationCode, row.Type);
            Assert.NotNull(row.Id);
            return new NativeTokenSnapshot(row.Id, scope.Owner.Database.Entry(row).CurrentValues.Clone(), clientState);
        }, token));

    /// <summary>Reinstates only the captured row after independently requiring its prior committed redemption, under its real owning coordination lock.</summary>
    internal Task RestoreAsync(IServiceProvider root) => WriteAsync(root, async (scope, token) =>
    {
        await scope.Owner.RunAsync(async current =>
        {
            var row = await scope.Owner.Database.Set<OpenIddictEntityFrameworkCoreToken>().SingleAsync(value => value.Id == identifier, current);
            Assert.Equal(OpenIddictConstants.Statuses.Redeemed, row.Status);
            scope.Owner.Database.Entry(row).CurrentValues.SetValues(original);
            await scope.Owner.Database.SaveChangesAsync(current);
        }, token, write: true);
    });

    /// <summary>Uses the installed manager to invalidate the resurrected row while preserving its retained evidence and owning lock.</summary>
    internal Task RevokeAsync(IServiceProvider root) => WriteAsync(root, async (scope, token) =>
    {
        var manager = scope.Services.GetRequiredService<IOpenIddictTokenManager>();
        var row = await manager.FindByIdAsync(identifier, token);
        Assert.NotNull(row);
        Assert.True(await manager.TryRevokeAsync(row, token));
    });

    /// <summary>Rereads every scalar field after restoration; a status-only reset cannot satisfy the exact pre-consumption image oracle.</summary>
    internal Task RequireOriginalAsync(IServiceProvider root) => ReadAsync(root, (scope, token) => scope.Owner.RunAsync(async current =>
    {
        var row = await scope.Owner.Database.Set<OpenIddictEntityFrameworkCoreToken>().SingleAsync(value => value.Id == identifier, current);
        var actual = scope.Owner.Database.Entry(row).CurrentValues;
        Assert.True(original.Properties.All(property => Equals(original[property.Name], actual[property.Name])));
        return true;
    }, token));

    /// <summary>Independently checks the committed status without printing the row's payload or reference.</summary>
    internal Task RequireStatusAsync(IServiceProvider root, string expected) => ReadAsync(root, (scope, token) => scope.Owner.RunAsync(async current =>
    {
        var status = await scope.Owner.Database.Set<OpenIddictEntityFrameworkCoreToken>().AsNoTracking()
            .Where(row => row.Id == identifier).Select(row => row.Status).SingleAsync(current);
        Assert.Equal(expected, status);
        return true;
    }, token));

    /// <summary>Uses unchanged request/write bounds and actual Platform/Sessions coordination for the narrowly scoped fixture mutation.</summary>
    private Task<bool> WriteAsync(IServiceProvider root, Func<ProtocolStorageScope, CancellationToken, Task> change) => ReadAsync(root, async (scope, token) =>
    {
        await using var transaction = await ProtocolStorageTransaction.OpenAsync(scope.Owner);
        if (clientState)
        {
            await AdministratorProtocolLocks.AcquireClientStateAsync(scope.Owner, token);
        }
        else
        {
            await AdministratorProtocolLocks.AcquireProfileAsync(scope.Owner, token);
        }
        await change(scope, token);
        await transaction.CommitAsync(token);
        return true;
    });

    /// <summary>Starts fresh ownership and selects it for the dual-mode server; the client already registers only owned stores.</summary>
    private static Task<T> ReadAsync<T>(IServiceProvider root, Func<ProtocolStorageScope, CancellationToken, ValueTask<T>> read) =>
        new ProtocolStorageRequest(root.GetRequiredService<TimeProvider>(), TestContext.Current.CancellationToken)
            .RunAsync(root.GetRequiredService<IServiceScopeFactory>(), (scope, token) =>
            {
                scope.Services.GetService<ProtocolStorageSelection>()?.Select(ProtocolStorageMode.Owned);
                return read(scope, token);
            });
}
