using System.Security.Cryptography;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Administrator.Tests.Features.SessionRefresh;

/// <summary>Owns a disposable provider database and complete native test server, without changing production B3 registration.</summary>
internal sealed class NativeCleanupFixture(BootstrapDatabase database, ServiceProvider services, RSA signing, RSA encryption, RSA client) : IAsyncDisposable
{
    private readonly CancellationToken cancellation = TestContext.Current.CancellationToken;
    private const int KeyBits = 3072;
    private const string SigningKey = "cleanup-server-signing";
    private const string EncryptionKey = "cleanup-server-encryption";
    private const string ClientKey = "cleanup-client-signing";
    internal IServiceProvider Services => services;
    internal NativeCleanupFaults Faults => services.GetRequiredService<NativeCleanupFaults>();
    internal SecurityKey ClientSigning => new RsaSecurityKey(client) { KeyId = ClientKey };
    internal bool Rollback
    {
        get; set;
    }

    /// <summary>Creates real native crypto and owned stores; the fixture does not authenticate a human or claim B3 login admission.</summary>
    internal static async Task<NativeCleanupFixture> CreateAsync(BootstrapFixture fixture, string provider)
    {
        var database = await fixture.LeaseAsync(provider);
        var signing = RSA.Create(KeyBits);
        var encryption = RSA.Create(KeyBits);
        var client = RSA.Create(KeyBits);
        ServiceProvider? services = null;
        try
        {
            var registrations = new ServiceCollection().AddLogging(logging => logging.ClearProviders())
                .AddIdentityPersistence(DatabaseProviderParser.Parse(provider), database.RuntimeConnection)
                .AddProtocolStorageOwnership().AddSingleton<NativeCleanupFaults>();
            NativeCleanupProfile.Configure(registrations, new RsaSecurityKey(signing) { KeyId = SigningKey },
                new RsaSecurityKey(encryption) { KeyId = EncryptionKey });
            services = registrations.BuildServiceProvider(validateScopes: true);
            var result = new NativeCleanupFixture(database, services, signing, encryption, client);
            await result.RegisterAsync();
            return result;
        }
        catch
        {
            if (services is not null)
            {
                await services.DisposeAsync();
            }
            signing.Dispose();
            encryption.Dispose();
            client.Dispose();
            await database.DisposeAsync();
            throw;
        }
    }

    /// <summary>Registers only the public assertion key and explicit native permissions using a genuine committed manager write.</summary>
    private Task<bool> RegisterAsync() => CommitAsync(async (scope, token) =>
    {
        var key = JsonWebKeyConverter.ConvertFromRSASecurityKey(new RsaSecurityKey(client.ExportParameters(false)) { KeyId = ClientKey });
        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = NativeCleanupProfile.Client,
            ClientType = OpenIddictConstants.ClientTypes.Confidential,
            JsonWebKeySet = new JsonWebKeySet(),
            RedirectUris = { new Uri(NativeCleanupProfile.Callback) },
            Permissions =
            {
                OpenIddictConstants.Permissions.Endpoints.Authorization,
                OpenIddictConstants.Permissions.Endpoints.Token,
                OpenIddictConstants.Permissions.Endpoints.Revocation,
                OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode,
                OpenIddictConstants.Permissions.GrantTypes.RefreshToken,
                OpenIddictConstants.Permissions.ResponseTypes.Code,
                OpenIddictConstants.Permissions.Prefixes.Scope + NativeCleanupProfile.Scope
            }
        };
        descriptor.JsonWebKeySet.Keys.Add(key);
        await scope.Services.GetRequiredService<IOpenIddictApplicationManager>().CreateAsync(descriptor, token);
        return true;
    });

    /// <summary>Runs one native request with a finite original owner and releases its result only after actual disposal.</summary>
    internal Task<T> RunAsync<T>(Func<ProtocolStorageScope, CancellationToken, ValueTask<T>> action, CancellationToken? requestToken = null) =>
        new ProtocolStorageRequest(TimeProvider.System, requestToken ?? cancellation)
            .RunAsync(services.GetRequiredService<IServiceScopeFactory>(), action);

    /// <summary>Commits all native writes together; an injected post-dispatch rollback never returns staged success bytes.</summary>
    internal Task<T> CommitAsync<T>(Func<ProtocolStorageScope, CancellationToken, ValueTask<T>> action, CancellationToken? requestToken = null) => RunAsync(async (scope, token) =>
    {
        await using var transaction = await ProtocolStorageTransaction.OpenAsync(scope.Owner);
        var result = await action(scope, token);
        if (Rollback)
        {
            Faults.ThrowRollback(result);
        }
        await transaction.CommitAsync(token);
        return result;
    }, requestToken);

    /// <summary>Releases native dependencies before private keys and the database lease.</summary>
    public async ValueTask DisposeAsync()
    {
        try
        {
            await services.DisposeAsync();
        }
        finally
        {
            signing.Dispose();
            encryption.Dispose();
            client.Dispose();
            await database.DisposeAsync();
        }
    }
}
