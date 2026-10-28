using IdentityPlatform.AuthServer.Features.Protocol.ClientAssertions;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace IdentityPlatform.Persistence.Tests.Features.ProtocolStorage;

/// <summary>Owns component observations between native client authentication and mandatory rejection of an invalid generic target.</summary>
internal sealed class CleanupAssertionFixture
{
    private const string RevocationPath = "/revoke";
    private const string InvalidTarget = "invalid-cleanup-characterization-target";
    private const string TargetHint = "cleanup-proof-characterization";
    private const int ObserverOffset = 1;
    private const int ProofLifetime = 60;
    internal IServiceProvider First
    {
        get;
    }
    internal IServiceProvider Second
    {
        get;
    }
    internal NativeAssertionMaterial Material
    {
        get;
    }
    internal DateTimeOffset Now
    {
        get;
    }

    /// <summary>Aligns only proof wall time across independently composed roots; request and operation timers remain monotonic.</summary>
    internal CleanupAssertionFixture(IServiceProvider first, IServiceProvider second, NativeAssertionMaterial material)
    {
        First = first;
        Second = second;
        Material = material;
        Now = first.GetRequiredService<NativeConsumptionClock>().Freeze();
        second.GetRequiredService<NativeConsumptionClock>().Set(Now);
    }

    /// <summary>Explicitly installs dormant cleanup proof code and a passive observer without disabling any native validation.</summary>
    internal static void Configure(IServiceCollection services, OpenIddictServerBuilder server)
    {
        server.SetRevocationEndpointUris(RevocationPath).AddAdministratorCleanupAssertions();
        server.AddEventHandler<OpenIddictServerEvents.ProcessAuthenticationContext>(builder => builder
            .SetOrder(IdentityPlatform.AuthServer.Features.Protocol.ClientAssertions.Setup.CleanupConsumptionOrder + ObserverOffset)
            .UseInlineHandler(CleanupAssertionProbe.ObserveAsync));
        services.AddSingleton<CleanupAssertionDisposalFault>();
        services.AddScoped<CleanupAssertionDisposal>();
        services.AddDbContext<PlatformDbContext>((provider, options) => { _ = provider.GetRequiredService<CleanupAssertionDisposal>(); });
    }

    /// <summary>Signs real fresh assertions while retaining only their independently computed persisted digest as an oracle.</summary>
    internal CleanupProof Proof(int lifetime = ProofLifetime)
    {
        var identifier = Guid.NewGuid().ToString();
        var now = Now.ToUnixTimeSeconds();
        return new(Material.Sign(now, identifier, lifetime), AdministratorAssertion.Create(identifier, now, now + lifetime, null)!.Digest);
    }

    /// <summary>Dispatches the unchanged native authentication pipeline; the final invalid target cannot become a successful revocation.</summary>
    internal static Task<OpenIddictServerEvents.ProcessAuthenticationContext> AuthenticateAsync(IServiceProvider root, string assertion,
        CleanupAssertionProbe probe, CancellationToken token) =>
        new ProtocolStorageRequest(root.GetRequiredService<TimeProvider>(), token).RunAsync(root.GetRequiredService<IServiceScopeFactory>(), async (scope, cancellation) =>
        {
            var transaction = await scope.Services.GetRequiredService<IOpenIddictServerFactory>().CreateTransactionAsync();
            transaction.EndpointType = OpenIddictServerEndpointType.Revocation;
            transaction.BaseUri = new(NativeAssertionMaterial.Issuer);
            transaction.RequestUri = new(new Uri(NativeAssertionMaterial.Issuer), RevocationPath);
            transaction.Request = new()
            {
                ClientId = AdministratorProtocolIdentity.ClientId,
                ClientAssertion = assertion,
                ClientAssertionType = OpenIddictConstants.ClientAssertionTypes.JwtBearer,
                Token = InvalidTarget,
                TokenTypeHint = TargetHint
            };
            transaction.SetProperty(typeof(CleanupAssertionProbe).FullName!, probe);
            var context = new OpenIddictServerEvents.ProcessAuthenticationContext(transaction) { CancellationToken = cancellation };
            probe.BeforeDispatch?.Invoke(context);
            await scope.Services.GetRequiredService<IOpenIddictServerDispatcher>().DispatchAsync(context);
            Assert.False(context.IsRequestHandled);
            Assert.False(context.IsRequestSkipped);
            if (probe.AfterTarget is not null)
            {
                await probe.AfterTarget(context, cancellation);
            }
            return context;
        });

    /// <summary>Observes the exact digest from an independent fresh owned context, never from a tracked handler entity.</summary>
    internal static Task<bool> RetainedAsync(IServiceProvider root, string digest, CancellationToken token) => ReadAsync(root,
        (database, current) => database.Set<AdministratorClientAssertionUse>().AsNoTracking().AnyAsync(row => row.Digest == digest, current), token);

    /// <summary>Counts all retained rows so rejection cannot silently evict unrelated replay tombstones.</summary>
    internal static Task<int> CountAsync(IServiceProvider root, CancellationToken token) => ReadAsync(root,
        (database, current) => database.Set<AdministratorClientAssertionUse>().CountAsync(current), token);

    /// <summary>Reads committed state through the original request budget and actual provider-owned scope.</summary>
    internal static Task<T> ReadAsync<T>(IServiceProvider root, Func<DbContext, CancellationToken, Task<T>> read, CancellationToken token) =>
        new ProtocolStorageRequest(root.GetRequiredService<TimeProvider>(), token).RunAsync(root.GetRequiredService<IServiceScopeFactory>(),
            (scope, cancellation) => scope.Owner.RunAsync(current => new ValueTask<T>(read(scope.Owner.Database, current)), cancellation));

    /// <summary>Changes only explicit synthetic fixture state under the actual profile lock and owning transaction.</summary>
    internal static Task<bool> MutateAsync(IServiceProvider root, Func<ProtocolStorageScope, CancellationToken, Task> change, CancellationToken token) =>
        new ProtocolStorageRequest(root.GetRequiredService<TimeProvider>(), token).RunAsync(root.GetRequiredService<IServiceScopeFactory>(), async (scope, cancellation) =>
        {
            await using var transaction = await ProtocolStorageTransaction.OpenAsync(scope.Owner);
            await AdministratorProtocolLocks.AcquireProfileAsync(scope.Owner, cancellation);
            await change(scope, cancellation);
            await transaction.CommitAsync(cancellation);
            return true;
        });

    /// <summary>Changes the central issuance flag only, preserving the exact native registration and shared replay rows.</summary>
    internal static Task<bool> SetEnabledAsync(IServiceProvider root, bool enabled, CancellationToken token) => MutateAsync(root,
        (scope, current) => scope.Owner.RunAsync(async cancellation =>
        {
            var profile = await scope.Owner.Database.Set<AdministratorProtocolProfile>().SingleAsync(cancellation);
            profile.Enabled = enabled;
            await scope.Owner.Database.SaveChangesAsync(cancellation);
        }, current, write: true).AsTask(), token);
}

/// <summary>Retains synthetic proof material without a generated credential-bearing ToString implementation.</summary>
internal sealed class CleanupProof(string text, string digest)
{
    internal string Text => text;
    internal string Digest => digest;
}
