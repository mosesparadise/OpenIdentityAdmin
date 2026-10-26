using System.Diagnostics;
using System.Globalization;
using System.Security.Claims;
using IdentityPlatform.Admin.Bff.Features.Authentication.Ownership;
using IdentityPlatform.Database;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using IdentityPlatform.Infrastructure.Features.Sessions.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using IdentityPlatform.Persistence.Tests.Features.ProviderCompatibility;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;
using OpenIddict.Client;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Persistence.Tests.Features.ProtocolStorage;

/// <summary>Composes an isolated Sessions client for actual native encrypted-state generation without a remote exchange.</summary>
internal static class NativeClientStateFixture
{
    private static readonly System.Text.CompositeFormat TimingFormat = System.Text.CompositeFormat.Parse("native-client-preparation {0} elapsed-ms={1:F0}");
    private const string Client = "identity-admin-bff";
    private const string Issuer = "https://issuer.example/";
    private const string ClientUri = "https://client.example/";
    private const string Callback = "https://client.example/callback";
    private const string Nonce = "native-state-nonce";
    private const string Verifier = "native-state-verifier";
    private const string Correlation = "native-state-correlation";
    private const int LifetimeSeconds = 300;

    /// <summary>Uses only the BFF runtime principal and Sessions mapping, with disposable test-only state credentials.</summary>
    internal static ServiceProvider Create(NativeProviderFixture fixture, NativeClientStateFault fault)
    {
        var services = new ServiceCollection().AddLogging().AddSessionPersistence(fixture.Provider, fixture.RuntimeConnection(DatabaseStore.Sessions));
        services.AddSingleton(fault);
        services.AddSingleton<NativeClientStateQueries>();
        services.AddDbContext<AdminSessionDbContext>((provider, options) => options.AddInterceptors(provider.GetRequiredService<NativeClientStateQueries>()));
        services.AddScoped<NativeClientStateCleanup>();
        services.AddSingleton<TimeProvider>(TimeProvider.System);
        services.AddProtocolStorageOwnership();
        services.AddOwnedClientDispatcher();
        services.AddOpenIddict().AddCore(core => { core.UseEntityFrameworkCore().UseDbContext<AdminSessionDbContext>(); core.DisableEntityCaching(); });
        var client = services.AddOpenIddict().AddClient().AllowAuthorizationCodeFlow().SetRedirectionEndpointUris(Callback)
            .AddEphemeralEncryptionKey().AddEphemeralSigningKey().AddDurableClientState();
        client.Configure(options => { options.ClientUri = new(ClientUri); options.StateTokenLifetime = TimeSpan.FromSeconds(LifetimeSeconds); });
        client.AddEventHandler<OpenIddictClientEvents.GenerateTokenContext>(builder => builder.UseScopedHandler<NativeClientStateFaultHandler>()
            .SetOrder(OpenIddictClientHandlers.Protection.GenerateIdentityModelToken.Descriptor.Order - 1));
        client.AddEventHandler<OpenIddictClientEvents.GenerateTokenContext>(builder => builder.UseScopedHandler<NativeClientStateStoredFault>()
            .SetOrder(OpenIddictClientHandlers.Protection.AttachTokenPayload.Descriptor.Order + 1));
        return Ready(services);
    }

    /// <summary>Finalizes real native options and ephemeral credentials during fixture setup, without generating state or renewing any request budget.</summary>
    private static ServiceProvider Ready(IServiceCollection services)
    {
        var root = services.BuildServiceProvider(validateScopes: true);
        try
        {
            var started = Stopwatch.GetTimestamp();
            _ = root.GetRequiredService<IOptionsMonitor<OpenIddictClientOptions>>().CurrentValue;
            Timing(PreparationPhase.OptionsReady, started);
            return root;
        }
        catch { root.Dispose(); throw; }
    }

    /// <summary>Retains only a code-owned phase and monotonic duration; an output failure never replaces the operation result.</summary>
    private static void Timing(PreparationPhase phase, long started)
    {
        try
        {
            TestContext.Current.TestOutputHelper?.WriteLine(string.Format(CultureInfo.InvariantCulture, TimingFormat,
                phase, Stopwatch.GetElapsedTime(started).TotalMilliseconds));
        }
        catch (Exception) { /* Diagnostics cannot alter the original request or fixture outcome. */ }
    }

    /// <summary>Names preparation boundaries without accepting caller-defined text or native request values.</summary>
    private enum PreparationPhase
    {
        OptionsReady, FactoryReady, ChildEntry
    }

    /// <summary>Invokes actual native state creation with a prepared principal; no callback/correlation interoperability is claimed.</summary>
    internal static Task<string> GenerateAsync(IServiceProvider root, CancellationToken token) =>
        new ProtocolStorageRequest(root.GetRequiredService<TimeProvider>(), token).RunAsync(root.GetRequiredService<IServiceScopeFactory>(), async (scope, cancellation) =>
        {
            var started = Stopwatch.GetTimestamp();
            var transaction = await scope.Services.GetRequiredService<IOpenIddictClientFactory>().CreateTransactionAsync();
            Timing(PreparationPhase.FactoryReady, started);
            transaction.Request = new();
            transaction.Registration = new()
            {
                RegistrationId = Client,
                ClientId = Client,
                Issuer = new(Issuer),
                RedirectUri = new(Callback)
            };
            var now = DateTimeOffset.FromUnixTimeSeconds(scope.Owner.Clock.GetUtcNow().ToUnixTimeSeconds());
            var principal = new ClaimsPrincipal(new ClaimsIdentity()).SetCreationDate(now).SetExpirationDate(now.AddSeconds(LifetimeSeconds))
                .SetClaim(OpenIddictConstants.Claims.Private.RegistrationId, Client).SetClaim(OpenIddictConstants.Claims.Private.Issuer, ClientUri)
                .SetClaim(OpenIddictConstants.Claims.Private.Nonce, Nonce).SetClaim(OpenIddictConstants.Claims.Private.CodeVerifier, Verifier)
                .SetClaim(OpenIddictConstants.Claims.RequestForgeryProtection, Correlation);
            var context = new OpenIddictClientEvents.ProcessChallengeContext(transaction)
            {
                StateTokenPrincipal = principal,
                GenerateStateToken = true,
                IncludeStateToken = true,
                ClientId = Client,
                RedirectUri = Callback,
                GrantType = OpenIddictConstants.GrantTypes.AuthorizationCode,
                ResponseType = OpenIddictConstants.ResponseTypes.Code,
                ResponseMode = OpenIddictConstants.ResponseModes.Query
            };
            root.GetRequiredService<NativeClientStateFault>().Challenge = context;
            Timing(PreparationPhase.ChildEntry, started);
            await scope.Services.GetRequiredService<DurableClientState>().HandleAsync(context);
            Assert.Null(NativeStateGeneration.Find(transaction));
            Assert.Null(scope.Owner.Database.Database.CurrentTransaction);
            return Assert.IsType<string>(context.StateToken);
        });

    /// <summary>Resolves the real opaque reference and validates its protected payload through native handlers in a fresh independent request.</summary>
    internal static Task<bool> ValidateAsync(IServiceProvider root, string state, CancellationToken token) =>
        new ProtocolStorageRequest(root.GetRequiredService<TimeProvider>(), token).RunAsync(root.GetRequiredService<IServiceScopeFactory>(), async (scope, cancellation) =>
        {
            var transaction = await scope.Services.GetRequiredService<IOpenIddictClientFactory>().CreateTransactionAsync();
            transaction.BaseUri = new(ClientUri);
            var context = new OpenIddictClientEvents.ValidateTokenContext(transaction) { Token = state };
            context.ValidTokenTypes.Add(OpenIddictConstants.TokenTypeIdentifiers.Private.StateToken);
            await scope.Services.GetRequiredService<IOpenIddictClientDispatcher>().DispatchAsync(context);
            Assert.False(context.IsRejected);
            Assert.False(context.IsRequestHandled);
            Assert.False(context.IsRequestSkipped);
            var principal = Assert.IsType<ClaimsPrincipal>(context.Principal);
            Assert.Equal(Nonce, principal.GetClaim(OpenIddictConstants.Claims.Private.Nonce));
            Assert.Equal(Verifier, principal.GetClaim(OpenIddictConstants.Claims.Private.CodeVerifier));
            Assert.Equal(Client, principal.GetClaim(OpenIddictConstants.Claims.Private.RegistrationId));
            Assert.Equal(Correlation, principal.GetClaim(OpenIddictConstants.Claims.RequestForgeryProtection));
            Assert.False(string.IsNullOrEmpty(principal.GetTokenId()));
            Assert.NotNull(principal.GetCreationDate());
            Assert.NotNull(principal.GetExpirationDate());
            return true;
        });

    /// <summary>Reads actual committed native state through a separate request/context after all generation cleanup has completed.</summary>
    internal static Task<int> CountAsync(IServiceProvider root, CancellationToken token) =>
        new ProtocolStorageRequest(root.GetRequiredService<TimeProvider>(), token).RunAsync(root.GetRequiredService<IServiceScopeFactory>(), (scope, cancellation) =>
            scope.Owner.RunAsync(current => new ValueTask<int>(scope.Owner.Database.Set<OpenIddictEntityFrameworkCoreToken>().CountAsync(current)), cancellation));

    /// <summary>Clears only this disposable fixture's Sessions native rows before an independent creation scenario.</summary>
    internal static Task<int> ClearAsync(IServiceProvider root, CancellationToken token) =>
        new ProtocolStorageRequest(root.GetRequiredService<TimeProvider>(), token).RunAsync(root.GetRequiredService<IServiceScopeFactory>(), async (scope, cancellation) =>
        {
            await using var transaction = await ProtocolStorageTransaction.OpenAsync(scope.Owner);
            await AdministratorProtocolLocks.AcquireClientStateAsync(scope.Owner, cancellation);
            var count = await scope.Owner.RunAsync(current => new ValueTask<int>(scope.Owner.Database.Set<OpenIddictEntityFrameworkCoreToken>().ExecuteDeleteAsync(current)), cancellation, write: true);
            await transaction.CommitAsync(cancellation);
            return count;
        });
}
