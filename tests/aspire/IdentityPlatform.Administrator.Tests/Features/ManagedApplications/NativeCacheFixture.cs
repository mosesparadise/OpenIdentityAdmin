using System.Security.Cryptography;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Administrator.Tests.Features.ManagedApplications;

/// <summary>Owns an actual provider database and native caching server with synthetic keys; it proves no production managed-client admission.</summary>
internal sealed class NativeCacheFixture(BootstrapDatabase database, ServiceProvider services, RSA signing, RSA encryption) : IAsyncDisposable
{
    private const int KeyBits = 3072;
    private const string SigningKey = "cache-server-signing";
    private const string EncryptionKey = "cache-server-encryption";
    internal string Secret { get; } = NativeCacheProfile.Random();
    internal NativeCacheFault Fault => services.GetRequiredService<NativeCacheFault>();

    /// <summary>Registers the installed native managers, owned stores and complete server; every dependency is disposed if setup fails.</summary>
    internal static async Task<NativeCacheFixture> CreateAsync(BootstrapFixture fixture, string provider)
    {
        var database = await fixture.LeaseAsync(provider);
        var signing = RSA.Create(KeyBits);
        var encryption = RSA.Create(KeyBits);
        ServiceProvider? services = null;
        try
        {
            var registrations = new ServiceCollection().AddLogging(logging => logging.ClearProviders())
                .AddIdentityPersistence(DatabaseProviderParser.Parse(provider), database.RuntimeConnection)
                .AddProtocolStorageOwnership().AddSingleton<NativeCacheFault>();
            NativeCacheProfile.Configure(registrations, new RsaSecurityKey(signing) { KeyId = SigningKey },
                new RsaSecurityKey(encryption) { KeyId = EncryptionKey });
            services = registrations.BuildServiceProvider(validateScopes: true);
            var result = new NativeCacheFixture(database, services, signing, encryption);
            await result.RegisterAsync(NativeCacheProfile.Client, NativeCacheProfile.Callback);
            await result.RegisterAsync(NativeCacheProfile.SecondClient, NativeCacheProfile.SecondCallback);
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
            await database.DisposeAsync();
            throw;
        }
    }

    /// <summary>Creates one real confidential client with the native secret hasher and only code/OpenID permissions.</summary>
    private Task<bool> RegisterAsync(string client, string callback) => CommitAsync(async (scope, token) =>
    {
        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = client,
            ClientType = OpenIddictConstants.ClientTypes.Confidential,
            ClientSecret = Secret,
            RedirectUris = { new Uri(callback) },
            Permissions =
            {
                OpenIddictConstants.Permissions.Endpoints.Authorization,
                OpenIddictConstants.Permissions.Endpoints.Token,
                OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode,
                OpenIddictConstants.Permissions.ResponseTypes.Code
            }
        };
        await scope.Services.GetRequiredService<IOpenIddictApplicationManager>().CreateAsync(descriptor, token);
        return true;
    });

    /// <summary>Releases observations only after the original owned request and real scope cleanup finish.</summary>
    internal Task<T> ReadAsync<T>(Func<ProtocolStorageScope, CancellationToken, ValueTask<T>> action) =>
        new ProtocolStorageRequest(TimeProvider.System, TestContext.Current.CancellationToken)
            .RunAsync(services.GetRequiredService<IServiceScopeFactory>(), action);

    /// <summary>Intentionally characterizes one enclosing native transaction; exceptions roll back all its writes.</summary>
    internal Task<T> CommitAsync<T>(Func<ProtocolStorageScope, CancellationToken, ValueTask<T>> action) => ReadAsync(async (scope, token) =>
    {
        await using var transaction = await ProtocolStorageTransaction.OpenAsync(scope.Owner);
        var result = await action(scope, token);
        await transaction.CommitAsync(token);
        return result;
    });

    /// <summary>Releases native services before keys and the isolated real database.</summary>
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
            await database.DisposeAsync();
        }
    }
}
