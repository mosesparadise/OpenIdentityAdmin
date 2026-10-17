using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.EntityFrameworkCore;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Host.Tests.Features.ProtocolStorage;

/// <summary>Proves store selection is explicit and immutable before native managers capture scoped dependencies.</summary>
public sealed class StorageSelectionTests
{
    private const int InvalidMode = 99;

    /// <summary>All four native stores use the same declared scope mode without inspecting ambient absence or activation timing.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_ExplicitScopeMode_When_NativeStoresResolve_Then_AllStoresMatchTheFrozenMode(bool owned)
    {
        await using var probe = new StorageRequestProbe(Configure);
        await using var scope = probe.Root.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ProtocolStorageSelection>().Select(owned ? ProtocolStorageMode.Owned : ProtocolStorageMode.LegacyPilot);
        Assert.Equal(owned ? typeof(OwnedApplicationStore) : typeof(OpenIddictEntityFrameworkCoreApplicationStore),
            scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationStore<OpenIddictEntityFrameworkCoreApplication>>().GetType());
        Assert.Equal(owned ? typeof(OwnedAuthorizationStore) : typeof(OpenIddictEntityFrameworkCoreAuthorizationStore),
            scope.ServiceProvider.GetRequiredService<IOpenIddictAuthorizationStore<OpenIddictEntityFrameworkCoreAuthorization>>().GetType());
        Assert.Equal(owned ? typeof(OwnedScopeStore) : typeof(OpenIddictEntityFrameworkCoreScopeStore),
            scope.ServiceProvider.GetRequiredService<IOpenIddictScopeStore<OpenIddictEntityFrameworkCoreScope>>().GetType());
        Assert.Equal(owned ? typeof(OwnedTokenStore) : typeof(OpenIddictEntityFrameworkCoreTokenStore),
            scope.ServiceProvider.GetRequiredService<IOpenIddictTokenStore<OpenIddictEntityFrameworkCoreToken>>().GetType());
    }

    /// <summary>An early native manager cannot silently capture a legacy store and recover through a later owned selection.</summary>
    [Fact]
    public async Task Given_UnselectedScope_When_NativeManagerResolves_Then_ResolutionAndLaterSelectionFail()
    {
        await using var probe = new StorageRequestProbe(Configure);
        await using var scope = probe.Root.CreateAsyncScope();
        Assert.Throws<InvalidOperationException>(() => scope.ServiceProvider.GetRequiredService<IOpenIddictTokenManager>());
        Assert.Throws<InvalidOperationException>(() => scope.ServiceProvider.GetRequiredService<ProtocolStorageSelection>().Select(ProtocolStorageMode.Owned));
        Assert.Throws<InvalidOperationException>(() => scope.ServiceProvider.GetRequiredService<IOpenIddictTokenStore<OpenIddictEntityFrameworkCoreToken>>());
    }

    /// <summary>A repeated or conflicting selection after store capture is terminal even when downstream catches its exception.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_ResolvedStore_When_ModeIsSelectedAgain_Then_RequestCannotRecover(bool same)
    {
        await using var probe = new StorageRequestProbe(Configure);
        var request = new ProtocolStorageRequest(probe.Clock, TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<InvalidOperationException>(() => request.RunAsync(probe.Factory, (scope, _) =>
        {
            var selection = scope.Services.GetRequiredService<ProtocolStorageSelection>();
            selection.Select(ProtocolStorageMode.LegacyPilot);
            scope.Services.GetRequiredService<IOpenIddictTokenStore<OpenIddictEntityFrameworkCoreToken>>();
            Assert.Throws<InvalidOperationException>(() => selection.Select(same ? ProtocolStorageMode.LegacyPilot : ProtocolStorageMode.Owned));
            return new ValueTask<bool>(true);
        }));
    }

    /// <summary>A protected continuation explicitly selects owned stores even when its parent used the approved pilot mode.</summary>
    [Fact]
    public async Task Given_LegacyPilotParent_When_FreshProtocolChildStarts_Then_ChildSelectsOwnedBeforeResolution()
    {
        await using var probe = new StorageRequestProbe(Configure);
        var request = new ProtocolStorageRequest(probe.Clock, TestContext.Current.CancellationToken);
        await request.RunAsync(probe.Factory, async (scope, _) =>
        {
            scope.Services.GetRequiredService<ProtocolStorageSelection>().Select(ProtocolStorageMode.LegacyPilot);
            Assert.IsType<OpenIddictEntityFrameworkCoreTokenStore>(scope.Services.GetRequiredService<IOpenIddictTokenStore<OpenIddictEntityFrameworkCoreToken>>());
            return await ProtocolStorageChild.RunAsync(probe.Factory, (child, _) =>
            {
                Assert.IsType<OwnedTokenStore>(child.Services.GetRequiredService<IOpenIddictTokenStore<OpenIddictEntityFrameworkCoreToken>>());
                return new ValueTask<bool>(true);
            });
        });
    }

    /// <summary>No undefined enum value may fall through to the legacy native-store branch.</summary>
    [Fact]
    public async Task Given_UndefinedMode_When_Selected_Then_TheScopeFailsClosed()
    {
        await using var probe = new StorageRequestProbe(Configure);
        await using var scope = probe.Root.CreateAsyncScope();
        Assert.Throws<InvalidOperationException>(() => scope.ServiceProvider.GetRequiredService<ProtocolStorageSelection>().Select((ProtocolStorageMode)InvalidMode));
    }

    /// <summary>Installs actual native manager/store resolution while the fixture context remains unopened.</summary>
    private static void Configure(IServiceCollection services)
    {
        services.AddOpenIddict().AddCore(options => options.UseEntityFrameworkCore());
        services.AddProtocolStorageSelection();
    }
}
