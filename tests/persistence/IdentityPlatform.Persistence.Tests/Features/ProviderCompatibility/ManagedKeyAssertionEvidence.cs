using System.Security.Claims;
using IdentityPlatform.AuthServer.Features.Protocol.ClientAssertions;
using IdentityPlatform.AuthServer.Features.Protocol.Ownership;
using IdentityPlatform.Infrastructure.Features.Applications;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Persistence.Tests.Features.ProtocolStorage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace IdentityPlatform.Persistence.Tests.Features.ProviderCompatibility;

/// <summary>Verifies native-key registration and durable one-use consumption with genuinely signed client assertions on each provider.</summary>
internal static class ManagedKeyAssertionEvidence
{
    private const string Client = "managed-orders-signing-service";
    private const string Name = "Orders signing service";
    private const int ConcurrentRequests = 2;
    private const int CredentialSeconds = 600;

    private const string TokenPath = "/connect/token";

    /// <summary>Composes the pinned native authentication dispatcher with owned stores and managed assertion consumption.</summary>
    internal static void Configure(IServiceCollection services)
    {
        services.AddOwnedServerDispatcher();
        var server = services.AddOpenIddict().AddServer().SetIssuer(new Uri(NativeAssertionMaterial.Issuer))
            .SetTokenEndpointUris(TokenPath).AllowClientCredentialsFlow().AddEphemeralSigningKey().AddEphemeralEncryptionKey().AddManagedAssertions();
        server.UseAspNetCore();
        services.AddOptions<OpenIddictServerOptions>().Configure<TimeProvider>((options, clock) => options.TimeProvider = clock);
    }

    /// <summary>Resolves the persisted native key, verifies the signed JWT and observes one accepted use across two fresh scopes.</summary>
    internal static async Task RequireAsync(ManagedSchemaFixture fixture)
    {
        var factory = fixture.Services.GetRequiredService<IServiceScopeFactory>();
        using var key = new NativeAssertionMaterial();
        var now = fixture.Services.GetRequiredService<TimeProvider>().GetUtcNow().ToUnixTimeSeconds();
        var native = await RegisterAsync(factory, key, now, fixture.Token);
        var proof = await VerifyAsync(key.Sign(now, clientId: Client), native);
        await using var first = await ProtocolStorageScope.OpenAsync(factory, fixture.Token);
        var receipt = await ApplicationAssertionLedger.ConsumeAsync(first, proof, fixture.Token);
        Assert.NotNull(receipt);
        Assert.Equal(key.Policy.Key.Fingerprint, receipt.Fingerprint);
        Assert.True(receipt.Allows(fixture.Services.GetRequiredService<TimeProvider>(), now));
        await using var second = await ProtocolStorageScope.OpenAsync(factory, fixture.Token);
        Assert.Null(await ApplicationAssertionLedger.ConsumeAsync(second, proof, fixture.Token));
        await RequireNativeAsync(fixture, key, now);
    }

    /// <summary>Submits the same genuine compact assertion to two native authentication requests and requires exactly one committed acceptance.</summary>
    private static async Task RequireNativeAsync(ManagedSchemaFixture fixture, NativeAssertionMaterial key, long now)
    {
        var compact = key.Sign(now, clientId: Client);
        var pair = await Task.WhenAll(Enumerable.Range(0, ConcurrentRequests).Select(_ =>
            NativeAssertionAuthentication.AuthenticateAsync(fixture.Services, compact, true, fixture.Token, machineClient: Client)));
        var accepted = Assert.Single(pair, context => !context.IsRejected);
        Assert.NotNull(accepted.ClientAssertionPrincipal);
        Assert.NotNull(ManagedAssertionReceipt.Find(accepted.Transaction));
        Assert.Equal(OpenIddictConstants.Errors.InvalidClient, Assert.Single(pair, context => context.IsRejected).Error);
    }

    /// <summary>Creates the actual OpenIddict confidential key registration and commits its approval before reading the issuer's public verification key.</summary>
    private static async Task<JsonWebKey> RegisterAsync(IServiceScopeFactory factory, NativeAssertionMaterial material, long now, CancellationToken token)
    {
        await using var scope = await ProtocolStorageScope.OpenAsync(factory, token);
        await using var transaction = await ProtocolStorageTransaction.OpenAsync(scope.Owner);
        var manager = scope.Services.GetRequiredService<IOpenIddictApplicationManager>();
        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = Client,
            DisplayName = Name,
            ApplicationType = OpenIddictConstants.ApplicationTypes.Web,
            ClientType = OpenIddictConstants.ClientTypes.Confidential,
            ConsentType = OpenIddictConstants.ConsentTypes.Explicit,
            JsonWebKeySet = new JsonWebKeySet()
        };
        descriptor.Permissions.UnionWith([OpenIddictConstants.Permissions.Endpoints.Token, OpenIddictConstants.Permissions.GrantTypes.ClientCredentials]);
        descriptor.JsonWebKeySet.Keys.Add(material.Policy.Key.CreateNativeKey());
        var app = await manager.CreateAsync(descriptor, token);
        var policy = new ApplicationPolicy
        {
            ApplicationId = (await manager.GetIdAsync(app, token))!,
            Profile = ApplicationKeyApproval.MachineProfile,
            Revision = 1,
            CredentialRevision = 1,
            CredentialIssuedAt = now,
            CredentialExpiresAt = now + CredentialSeconds,
            ObservedAt = now
        };
        await scope.Owner.RunAsync(async current =>
        {
            scope.Owner.Database.Add(policy);
            scope.Owner.Database.Add(ApplicationKeyApproval.Create(policy, material.Policy.Key));
            await scope.Owner.Database.SaveChangesAsync(current);
        }, token, write: true);
        var keys = await manager.GetJsonWebKeySetAsync(app, token);
        var result = Assert.Single(keys!.Keys);
        await transaction.CommitAsync(token);
        return result;
    }

    /// <summary>Uses the library's RS256 validation result and the original compact bytes to obtain the bounded managed assertion claims.</summary>
    private static async Task<SignedClientAssertion> VerifyAsync(string compact, JsonWebKey native)
    {
        var validation = await new JsonWebTokenHandler().ValidateTokenAsync(compact, new TokenValidationParameters
        {
            IssuerSigningKey = native,
            ValidateIssuerSigningKey = true,
            RequireSignedTokens = true,
            ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
            ValidateIssuer = true,
            ValidIssuer = Client,
            ValidateAudience = true,
            ValidAudience = NativeAssertionMaterial.Issuer,
            RequireExpirationTime = true,
            ValidateLifetime = true
        });
        Assert.True(validation.IsValid);
        var context = new OpenIddictServerEvents.ValidateTokenContext(new OpenIddictServerTransaction { Logger = NullLogger.Instance })
        {
            Token = compact,
            TokenValidationResult = validation,
            Principal = new ClaimsPrincipal(validation.ClaimsIdentity)
                .SetTokenType(OpenIddictConstants.TokenTypeIdentifiers.Private.ClientAssertion),
            ValidTokenTypes = { OpenIddictConstants.TokenTypeIdentifiers.Private.ClientAssertion }
        };
        return Assert.IsType<SignedClientAssertion>(ClientAssertionShape.ReadClient(context, NativeAssertionMaterial.Issuer, native.Kid, Client));
    }
}
