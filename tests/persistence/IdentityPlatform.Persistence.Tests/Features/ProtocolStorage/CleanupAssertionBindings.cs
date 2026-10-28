using IdentityPlatform.AuthServer.Features.Protocol.ClientAssertions;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenIddict.Server;

namespace IdentityPlatform.Persistence.Tests.Features.ProtocolStorage;

/// <summary>Proves that only a current B3 cleanup read can admit the disabled ledger path and that default composition remains dormant.</summary>
internal static class CleanupAssertionBindings
{
    private const int ProofLifetime = 60;

    /// <summary>Checks additive configuration on actual resolved native options, including the existing base-only fixtures.</summary>
    internal static void RequireComposition(IServiceProvider root, bool cleanup)
    {
        var handlers = root.GetRequiredService<IOptionsMonitor<OpenIddictServerOptions>>().CurrentValue.Handlers;
        Assert.Single(handlers, handler => handler.ServiceDescriptor.ServiceType == typeof(AdministratorAssertionCapture));
        Assert.Single(handlers, handler => handler.ServiceDescriptor.ServiceType == typeof(AdministratorAssertionGuard));
        Assert.Single(handlers, handler => handler.ServiceDescriptor.ServiceType == typeof(AdministratorClientAssertion));
        Assert.Equal(cleanup ? 1 : 0, handlers.Count(handler => handler.ServiceDescriptor.ServiceType == typeof(AdministratorCleanupAssertion)));
        Assert.Equal(cleanup ? 1 : 0, handlers.Count(handler => handler.ServiceDescriptor.ServiceType == typeof(AdministratorCleanupAssertionGuard)));
    }

    /// <summary>Rejects B2, a completed write epoch and another request's otherwise genuine cleanup binding.</summary>
    internal static async Task VerifyAsync(CleanupAssertionFixture fixture, CancellationToken token)
    {
        await StalePhaseAsync(fixture, token);
        await ForeignRequestAsync(fixture, token);
        await CleanupAssertionEligibility.AcceptedAsync(fixture.First, fixture.Proof().Text, token);
    }

    /// <summary>An actually initialized exact B2 registration admits its ordinary reader but never mints the cleanup-specific capability.</summary>
    internal static Task<bool> RequireB2DeniedAsync(IServiceProvider root, AdministratorRegistrationPolicy b2, CancellationToken token) =>
        CleanupAssertionFixture.MutateAsync(root, async (scope, cancellation) =>
        {
            Assert.False(b2.IsBrowserSession);
            Assert.NotNull(await AdministratorRegistration.ReadLockedAsync(scope, b2, cancellation));
            Assert.Null(await AdministratorRegistration.ReadCleanupLockedAsync(scope, b2, cancellation));
        }, token);

    /// <summary>The same owner cannot carry a genuine cleanup binding from its completed epoch into a fresh one.</summary>
    private static async Task StalePhaseAsync(CleanupAssertionFixture fixture, CancellationToken token)
    {
        var proof = Assertion(fixture);
        var reached = 0;
        await Assert.ThrowsAnyAsync<InvalidOperationException>(() => new ProtocolStorageRequest(fixture.First.GetRequiredService<TimeProvider>(), token)
            .RunAsync(fixture.First.GetRequiredService<IServiceScopeFactory>(), async (scope, cancellation) =>
            {
                AdministratorCleanupRegistration? stale;
                await using (var first = await ProtocolStorageTransaction.OpenAsync(scope.Owner))
                {
                    await AdministratorProtocolLocks.AcquireProfileAsync(scope.Owner, cancellation);
                    stale = await AdministratorRegistration.ReadCleanupLockedAsync(scope, fixture.Material.Policy, cancellation);
                    Assert.NotNull(stale);
                }
                await using var second = await ProtocolStorageTransaction.OpenAsync(scope.Owner);
                reached++;
                stale.RequireCurrent(scope.Owner, cancellation);
                return true;
            }));
        Assert.Equal(1, reached);
        Assert.False(await CleanupAssertionFixture.RetainedAsync(fixture.Second, proof.Digest, token));
    }

    /// <summary>A binding minted by a disposed original request cannot admit another live root with matching database identities.</summary>
    private static async Task ForeignRequestAsync(CleanupAssertionFixture fixture, CancellationToken token)
    {
        AdministratorCleanupRegistration? foreign = null;
        await CleanupAssertionFixture.MutateAsync(fixture.First, async (scope, cancellation) =>
            foreign = await AdministratorRegistration.ReadCleanupLockedAsync(scope, fixture.Material.Policy, cancellation), token);
        Assert.NotNull(foreign);
        var proof = Assertion(fixture);
        var reached = 0;
        await Assert.ThrowsAnyAsync<InvalidOperationException>(() => CleanupAssertionFixture.MutateAsync(fixture.Second, async (scope, cancellation) =>
        {
            reached++;
            await AdministratorAssertionLedger.ReserveCleanupLockedAsync(scope.Owner, proof, foreign, cancellation);
        }, token));
        Assert.Equal(1, reached);
        Assert.False(await CleanupAssertionFixture.RetainedAsync(fixture.First, proof.Digest, token));
    }

    /// <summary>Creates bounded raw ledger input only for capability rejection tests, never as cryptographic acceptance evidence.</summary>
    private static AdministratorAssertion Assertion(CleanupAssertionFixture fixture) =>
        AdministratorAssertion.Create(Guid.NewGuid().ToString(), fixture.Now.ToUnixTimeSeconds(), fixture.Now.ToUnixTimeSeconds() + ProofLifetime, null)!;
}
