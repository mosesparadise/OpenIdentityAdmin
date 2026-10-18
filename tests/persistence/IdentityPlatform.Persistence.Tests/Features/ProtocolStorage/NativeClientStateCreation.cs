using IdentityPlatform.Persistence.Tests.Features.ProviderCompatibility;

namespace IdentityPlatform.Persistence.Tests.Features.ProtocolStorage;

/// <summary>Checks actual native state generation and fail-closed output with least-privilege Sessions credentials on each provider.</summary>
internal static class NativeClientStateCreation
{
    /// <summary>Preserves native crypto/storage for success and rejects injected crypto, persisted-row and cleanup substitutions.</summary>
    internal static async Task VerifyAsync(NativeProviderFixture fixture, CancellationToken token)
    {
        var fault = new NativeClientStateFault();
        await using var root = NativeClientStateFixture.Create(fixture, fault);
        foreach (var mode in Enum.GetValues<NativeClientStateFault.Kind>())
        {
            fault.Mode = NativeClientStateFault.Kind.None;
            await NativeClientStateFixture.ClearAsync(root, token);
            fault.Mode = mode;
            fault.Calls = 0;
            if (mode == NativeClientStateFault.Kind.None)
            {
                var state = await NativeClientStateFixture.GenerateAsync(root, token);
                Assert.False(string.IsNullOrEmpty(state));
                await NativeClientStateFixture.ValidateAsync(root, state, token);
            }
            else
            {
                await Assert.ThrowsAsync<InvalidOperationException>(() => NativeClientStateFixture.GenerateAsync(root, token));
                Assert.Equal(1, fault.Calls);
            }
            var expected = mode is NativeClientStateFault.Kind.None or NativeClientStateFault.Kind.ChangedCleanupPrincipal or
                NativeClientStateFault.Kind.ChangedCleanupParameters or NativeClientStateFault.Kind.RemovedCleanupState ? 1 : 0;
            Assert.Equal(expected, await NativeClientStateFixture.CountAsync(root, token));
        }
        fault.Mode = NativeClientStateFault.Kind.None;
        await NativeClientStateFixture.ClearAsync(root, token);
    }
}
