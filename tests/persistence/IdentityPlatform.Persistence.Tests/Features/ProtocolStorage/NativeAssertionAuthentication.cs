using IdentityPlatform.AuthServer.Features.Protocol.Administrator;
using IdentityPlatform.AuthServer.Features.Protocol.ClientAssertions;
using IdentityPlatform.AuthServer.Features.Protocol.Ownership;
using IdentityPlatform.Database;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using IdentityPlatform.Infrastructure.Persistence;
using IdentityPlatform.Persistence.Tests.Features.ProviderCompatibility;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace IdentityPlatform.Persistence.Tests.Features.ProtocolStorage;

/// <summary>Runs installed native assertion authentication and real replay persistence against two independent provider-backed instances.</summary>
internal static class NativeAssertionAuthentication
{
    private const string AuthorizePath = "/authorize";
    private const string TokenPath = "/token";
    private const string ParPath = "/par";
    private const string InvalidCode = "invalid-authorization-code";
    private const int ProofLifetime = 60;
    private const int ReservedBeforeBoundaries = 2;

    /// <summary>Requires native crypto success at PAR, shared replay denial at token and durability after native invalid-grant rejection.</summary>
    internal static async Task VerifyAsync(NativeProviderFixture fixture, NativeAssertionMaterial material, CancellationToken token)
    {
        await using var first = CreateRoot(fixture, material);
        await using var second = CreateRoot(fixture, material);
        var clock = first.GetRequiredService<NativeConsumptionClock>();
        var otherClock = second.GetRequiredService<NativeConsumptionClock>();
        var now = clock.Freeze();
        otherClock.Set(now);
        try
        {
            using var foreign = new NativeAssertionMaterial();
            Assert.Equal(OpenIddictConstants.Errors.InvalidClient,
                (await AuthenticateAsync(first, foreign.Sign(now.ToUnixTimeSeconds()), false, token)).Error);
            Assert.Equal(0, await CountAsync(second, token));
            var assertion = material.Sign(now.ToUnixTimeSeconds());
            var accepted = await AuthenticateAsync(first, assertion, false, token);
            Assert.False(accepted.IsRejected);
            Assert.NotNull(accepted.ClientAssertionPrincipal);
            Assert.Equal(1, await CountAsync(second, token));
            Assert.Equal(OpenIddictConstants.Errors.InvalidClient, (await AuthenticateAsync(second, assertion, true, token)).Error);
            var grant = material.Sign(now.ToUnixTimeSeconds());
            var invalid = await AuthenticateAsync(first, grant, true, token);
            Assert.Equal(OpenIddictConstants.Errors.InvalidToken, invalid.Error);
            invalid.Transaction.Response = new OpenIddictResponse { Error = invalid.Error };
            await new OpenIddictServerHandlers.Exchange.NormalizeErrorResponse().HandleAsync(new(invalid.Transaction));
            Assert.Equal(OpenIddictConstants.Errors.InvalidGrant, invalid.Transaction.Response.Error);
            Assert.Equal(ReservedBeforeBoundaries, await CountAsync(second, token));
            Assert.Equal(OpenIddictConstants.Errors.InvalidClient, (await AuthenticateAsync(second, grant, false, token)).Error);
            await VerifyCommitBoundariesAsync(first, second, material, now, token);
            await NativeAssertionContinuations.VerifyAsync(first, material, now, token);
            await NativeAssertionCleanup.VerifyAsync(first, second, material, now, token);
            await NativeParIssuance.VerifyAsync(first, second, material, now, token);
        }
        finally
        {
            clock.Reset();
            otherClock.Reset();
            await ClearAsync(first, token);
        }
    }

    /// <summary>A lost commit acknowledgement or expiry during actual commit denies authentication while retaining the already consumed proof.</summary>
    private static async Task VerifyCommitBoundariesAsync(ServiceProvider first, ServiceProvider second,
        NativeAssertionMaterial material, DateTimeOffset now, CancellationToken token)
    {
        var commits = first.GetRequiredService<NativeConsumptionCommit>();
        var proof = material.Sign(now.ToUnixTimeSeconds());
        var expected = new ArithmeticException();
        var attempts = 0;
        commits.AfterCommit = () => { attempts++; throw expected; };
        try
        {
            Assert.Same(expected, await Assert.ThrowsAsync<ArithmeticException>(() => AuthenticateAsync(first, proof, false, token)));
        }
        finally { commits.AfterCommit = null; }
        Assert.Equal(1, attempts);
        Assert.Equal(OpenIddictConstants.Errors.InvalidClient, (await AuthenticateAsync(second, proof, false, token)).Error);
        proof = material.Sign(now.ToUnixTimeSeconds());
        commits.AfterCommit = () => first.GetRequiredService<NativeConsumptionClock>().Set(now.AddSeconds(ProofLifetime));
        try
        {
            Assert.Equal(OpenIddictConstants.Errors.InvalidClient, (await AuthenticateAsync(first, proof, false, token)).Error);
        }
        finally { commits.AfterCommit = null; first.GetRequiredService<NativeConsumptionClock>().Set(now); }
        Assert.Equal(OpenIddictConstants.Errors.InvalidClient, (await AuthenticateAsync(second, proof, false, token)).Error);
    }

    /// <summary>Uses actual native authentication and all default validation handlers; no prevalidated principal is injected.</summary>
    internal static Task<OpenIddictServerEvents.ProcessAuthenticationContext> AuthenticateAsync(IServiceProvider root, string assertion,
        bool tokenEndpoint, CancellationToken token,
        Func<OpenIddictServerEvents.ProcessAuthenticationContext, CancellationToken, ValueTask>? continuation = null) => new ProtocolStorageRequest(root.GetRequiredService<TimeProvider>(), token)
        .RunAsync(root.GetRequiredService<IServiceScopeFactory>(), async (scope, cancellation) =>
        {
            var transaction = await scope.Services.GetRequiredService<IOpenIddictServerFactory>().CreateTransactionAsync();
            transaction.EndpointType = tokenEndpoint ? OpenIddictServerEndpointType.Token : OpenIddictServerEndpointType.PushedAuthorization;
            transaction.Request = new OpenIddictRequest
            {
                ClientId = AdministratorProtocolIdentity.ClientId,
                ClientAssertion = assertion,
                ClientAssertionType = OpenIddictConstants.ClientAssertionTypes.JwtBearer,
                GrantType = tokenEndpoint ? OpenIddictConstants.GrantTypes.AuthorizationCode : null,
                Code = tokenEndpoint ? InvalidCode : null
            };
            transaction.BaseUri = new Uri(NativeAssertionMaterial.Issuer);
            transaction.RequestUri = new Uri(NativeAssertionMaterial.Issuer);
            var context = new OpenIddictServerEvents.ProcessAuthenticationContext(transaction) { CancellationToken = cancellation };
            await scope.Services.GetRequiredService<IOpenIddictServerDispatcher>().DispatchAsync(context);
            Assert.False(context.IsRequestHandled);
            Assert.False(context.IsRequestSkipped);
            if (continuation is not null)
            {
                await continuation(context, cancellation);
            }
            return context;
        });

    /// <summary>Installs the ordinary native server crypto pipeline and owned stores with the explicit dormant assertion adapters.</summary>
    private static ServiceProvider CreateRoot(NativeProviderFixture fixture, NativeAssertionMaterial material)
    {
        var services = new ServiceCollection().AddLogging().AddPlatformPersistence(fixture.Provider,
            fixture.RuntimeConnection(DatabaseStore.Platform), fixture.RuntimeConnection(DatabaseStore.Sessions));
        services.AddSingleton(material.Policy);
        services.AddSingleton<NativeConsumptionClock>();
        services.AddSingleton<TimeProvider>(provider => provider.GetRequiredService<NativeConsumptionClock>());
        services.AddSingleton<NativeConsumptionCommit>();
        services.AddSingleton<NativeParFault>();
        services.AddSingleton<NativeAssertionCleanupClock>();
        services.AddDbContext<PlatformDbContext>((provider, options) => options.AddInterceptors(provider.GetRequiredService<NativeConsumptionCommit>(),
            provider.GetRequiredService<NativeAssertionCleanupClock>()));
        services.AddProtocolStorageOwnership().AddOwnedServerDispatcher();
        var server = services.AddOpenIddict().AddServer().SetIssuer(new Uri(NativeAssertionMaterial.Issuer))
            .SetAuthorizationEndpointUris(AuthorizePath).SetTokenEndpointUris(TokenPath).SetPushedAuthorizationEndpointUris(ParPath)
            .AllowAuthorizationCodeFlow().AddEphemeralSigningKey().AddEphemeralEncryptionKey().AddAdministratorAssertions();
        server.UseAspNetCore();
        server.AddAdministratorParIssuance();
        NativeParFault.Configure(server);
        services.AddOptions<OpenIddictServerOptions>().Configure<TimeProvider>((options, clock) => options.TimeProvider = clock);
        return services.BuildServiceProvider(validateScopes: true);
    }

    /// <summary>Reads actual persisted use count through a separate owned context, independently of handler return values.</summary>
    private static Task<int> CountAsync(IServiceProvider root, CancellationToken token) =>
        new ProtocolStorageRequest(root.GetRequiredService<TimeProvider>(), token).RunAsync(root.GetRequiredService<IServiceScopeFactory>(),
            (scope, cancellation) => scope.Owner.RunAsync(current => new ValueTask<int>(scope.Owner.Database.Set<AdministratorClientAssertionUse>().CountAsync(current)), cancellation));

    /// <summary>Removes only this disposable fixture's replay rows before the separate exact-capacity scenario begins.</summary>
    private static Task<int> ClearAsync(IServiceProvider root, CancellationToken token) =>
        new ProtocolStorageRequest(root.GetRequiredService<TimeProvider>(), token).RunAsync(root.GetRequiredService<IServiceScopeFactory>(), async (scope, cancellation) =>
        {
            await using var transaction = await ProtocolStorageTransaction.OpenAsync(scope.Owner);
            await AdministratorProtocolLocks.AcquireProfileAsync(scope.Owner, cancellation);
            var count = await scope.Owner.RunAsync(current => new ValueTask<int>(scope.Owner.Database.Set<AdministratorClientAssertionUse>().ExecuteDeleteAsync(current)), cancellation, write: true);
            await transaction.CommitAsync(cancellation);
            return count;
        });
}
