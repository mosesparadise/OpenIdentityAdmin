using IdentityPlatform.AuthServer.Features.Protocol.ClientAssertions;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace IdentityPlatform.Persistence.Tests.Features.ProtocolStorage;

/// <summary>Proves denied real assertion authentication cannot commit speculative tombstone cleanup across a clock rollback.</summary>
internal static class NativeAssertionCleanup
{
    private const int ShortLifetime = 10;
    private const int ForwardSeconds = 50;

    /// <summary>Deletes a formerly expired short proof during replay of a longer proof, then requires rollback to retain its replay denial.</summary>
    internal static async Task VerifyAsync(IServiceProvider first, IServiceProvider second, NativeAssertionMaterial material,
        DateTimeOffset now, CancellationToken token)
    {
        var identifier = Guid.NewGuid().ToString();
        var shortProof = material.Sign(now.ToUnixTimeSeconds(), identifier, ShortLifetime);
        var digest = AdministratorAssertion.Create(identifier, now.ToUnixTimeSeconds(), now.ToUnixTimeSeconds() + ShortLifetime, null)!.Digest;
        var longProof = material.Sign(now.ToUnixTimeSeconds());
        Assert.False((await NativeAssertionAuthentication.AuthenticateAsync(first, shortProof, false, token)).IsRejected);
        Assert.False((await NativeAssertionAuthentication.AuthenticateAsync(first, longProof, false, token)).IsRejected);
        var clock = first.GetRequiredService<NativeConsumptionClock>();
        var fault = first.GetRequiredService<NativeAssertionCleanupClock>();
        var deleted = 0;
        fault.AfterDelete = () => { deleted++; clock.Set(now); };
        clock.Set(now.AddSeconds(ForwardSeconds));
        try
        {
            var denied = await NativeAssertionAuthentication.AuthenticateAsync(first, longProof, false, token);
            Assert.Equal(OpenIddictConstants.Errors.InvalidClient, denied.Error);
            Assert.Null(denied.Transaction.GetProperty<DurableClientAssertion>(typeof(DurableClientAssertion).FullName!));
            Assert.Equal(1, deleted);
            Assert.True(await RetainedAsync(second, digest, token));
            Assert.Equal(OpenIddictConstants.Errors.InvalidClient,
                (await NativeAssertionAuthentication.AuthenticateAsync(second, shortProof, false, token)).Error);
        }
        finally { fault.AfterDelete = null; clock.Set(now); }
    }

    /// <summary>Independently observes the exact short-proof tombstone, preventing unrelated authentication denial from satisfying the regression.</summary>
    private static Task<bool> RetainedAsync(IServiceProvider root, string digest, CancellationToken token) =>
        new ProtocolStorageRequest(root.GetRequiredService<TimeProvider>(), token).RunAsync(root.GetRequiredService<IServiceScopeFactory>(),
            (scope, cancellation) => scope.Owner.RunAsync(current => new ValueTask<bool>(scope.Owner.Database
                .Set<AdministratorClientAssertionUse>().AnyAsync(row => row.Digest == digest, current)), cancellation));
}
