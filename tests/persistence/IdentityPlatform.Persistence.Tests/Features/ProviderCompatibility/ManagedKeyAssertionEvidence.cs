using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using IdentityPlatform.AuthServer.Features.Protocol.ClientAssertions;
using IdentityPlatform.AuthServer.Features.Protocol.Ownership;
using IdentityPlatform.Core.Features.Applications;
using IdentityPlatform.Infrastructure.Features.Applications;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Persistence.Tests.Features.ProtocolStorage;
using Microsoft.EntityFrameworkCore;
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
    private const string Actor = "fixture-key-administrator";
    private const string Client = "managed-orders-signing-service";
    private const string Name = "Orders signing service";
    private const int AfterRetentionSeconds = 91;
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
        var (appId, native) = await RegisterAsync(factory, key, now, fixture.Token);
        var proof = await VerifyAsync(key.Sign(now, clientId: Client), native);
        await using var first = await ProtocolStorageScope.OpenAsync(factory, fixture.Token);
        var receipt = await ApplicationAssertionLedger.ConsumeAsync(first, proof, fixture.Token);
        Assert.NotNull(receipt);
        Assert.Equal(key.Policy.Key.Fingerprint, receipt.Fingerprint);
        Assert.True(receipt.Allows(fixture.Services.GetRequiredService<TimeProvider>(), now));
        await using var second = await ProtocolStorageScope.OpenAsync(factory, fixture.Token);
        Assert.Null(await ApplicationAssertionLedger.ConsumeAsync(second, proof, fixture.Token));
        var usedId = await RequireNativeAsync(fixture, key, now);
        await RequireReplacementAsync(fixture, appId, key, usedId, now);
    }

    /// <summary>Submits the same genuine compact assertion to two native authentication requests and requires exactly one committed acceptance.</summary>
    private static async Task<string> RequireNativeAsync(ManagedSchemaFixture fixture, NativeAssertionMaterial key, long now)
    {
        var identifier = Guid.NewGuid().ToString();
        var compact = key.Sign(now, identifier, clientId: Client);
        var pair = await Task.WhenAll(Enumerable.Range(0, ConcurrentRequests).Select(_ =>
            NativeAssertionAuthentication.AuthenticateAsync(fixture.Services, compact, true, fixture.Token, machineClient: Client)));
        var accepted = Assert.Single(pair, context => !context.IsRejected);
        Assert.NotNull(accepted.ClientAssertionPrincipal);
        Assert.NotNull(ManagedAssertionReceipt.Find(accepted.Transaction));
        Assert.Equal(OpenIddictConstants.Errors.InvalidClient, Assert.Single(pair, context => context.IsRejected).Error);
        return identifier;
    }

    /// <summary>Rotates actual native and product keys, then authenticates against the original replay history and the replacement key.</summary>
    private static async Task RequireReplacementAsync(ManagedSchemaFixture fixture, string id, NativeAssertionMaterial original, string usedId, long now)
    {
        using var replacement = new NativeAssertionMaterial();
        var factory = fixture.Services.GetRequiredService<IServiceScopeFactory>();
        await using (var child = await ProtocolStorageScope.OpenAsync(factory, fixture.Token))
        {
            await using var tx = await ProtocolStorageTransaction.OpenAsync(child.Owner);
            var pub = replacement.Policy.Key.CreateNativeKey();
            var form = JsonSerializer.Serialize(new
            {
                kty = pub.Kty,
                kid = pub.Kid,
                alg = pub.Alg,
                use = pub.Use,
                n = pub.N,
                e = pub.E
            });
            var observation = DateTimeOffset.FromUnixTimeSeconds(now);
            var expiry = observation.AddSeconds(CredentialSeconds).ToString(ApplicationEditorRules.ExpiryFormat, CultureInfo.InvariantCulture);
            var command = ApplicationMutationCommand.ReplaceKey(id, ApplicationVersion.Create(id, 1), form, expiry, true, observation)!;
            var state = await ApplicationMutationWriter.StageAsync(child, command, fixture.Token);
            var audit = await ApplicationMutationAudit.AuditAsync(child, command, state, Actor, Guid.NewGuid(), fixture.Token);
            await ApplicationMutationAudit.VerifyAuditAsync(child, command, state, Actor, audit, fixture.Token);
            await state.VerifyAsync(child, fixture.Token);
            await tx.CommitAsync(fixture.Token);
        }
        await RequireAuditsAsync(fixture, id, original.Policy.Key.Fingerprint, replacement.Policy.Key.Fingerprint);
        var old = await NativeAssertionAuthentication.AuthenticateAsync(fixture.Services, original.Sign(now, clientId: Client), true, fixture.Token, machineClient: Client);
        Assert.Equal(OpenIddictConstants.Errors.InvalidClient, old.Error);
        var spent = await NativeAssertionAuthentication.AuthenticateAsync(fixture.Services, replacement.Sign(now, usedId, clientId: Client), true, fixture.Token, machineClient: Client);
        Assert.Equal(OpenIddictConstants.Errors.InvalidClient, spent.Error);
        var fresh = await NativeAssertionAuthentication.AuthenticateAsync(fixture.Services, replacement.Sign(now, clientId: Client), true, fixture.Token, machineClient: Client);
        Assert.False(fresh.IsRejected);
        Assert.Equal(replacement.Policy.Key.Fingerprint, ManagedAssertionReceipt.Find(fresh.Transaction)!.Stored.Fingerprint);
        var clock = Assert.IsType<NativeConsumptionClock>(fixture.Services.GetRequiredService<TimeProvider>());
        var later = DateTimeOffset.FromUnixTimeSeconds(now + AfterRetentionSeconds);
        clock.Set(later);
        var reclaimed = await NativeAssertionAuthentication.AuthenticateAsync(fixture.Services, replacement.Sign(later.ToUnixTimeSeconds(), usedId, clientId: Client),
            true, fixture.Token, machineClient: Client);
        Assert.False(reclaimed.IsRejected);
        Assert.Equal(later.ToUnixTimeSeconds(), ManagedAssertionReceipt.Find(reclaimed.Transaction)!.Stored.ObservedAt);
    }

    /// <summary>Reads both committed audit transitions independently using their immutable application and actor, public fingerprints and credential revisions.</summary>
    private static async Task RequireAuditsAsync(ManagedSchemaFixture fixture, string id, string initial, string current)
    {
        const long ReplacementRevision = 2;
        const string Registered = "ApplicationPublicKeyRegistered";
        const string Replaced = "ApplicationPublicKeyReplaced";
        await using var scope = fixture.Scope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityPlatform.Infrastructure.Persistence.PlatformDbContext>();
        var rows = await (from detail in db.Set<ApplicationKeyAuditDetail>().AsNoTracking()
                          join audit in db.Set<SecurityAuditIntent>().AsNoTracking() on detail.AuditId equals audit.Id
                          where audit.TargetId == id && audit.ActorId == Actor
                          orderby detail.CredentialRevision
                          select new
                          {
                              audit.Event,
                              detail.PreviousRevision,
                              detail.CredentialRevision,
                              detail.Fingerprint
                          }).ToArrayAsync(fixture.Token);
        Assert.Collection(rows, first => Assert.Equal((Registered, 0L, 1L, initial), (first.Event, first.PreviousRevision, first.CredentialRevision, first.Fingerprint)),
            second => Assert.Equal((Replaced, 1L, ReplacementRevision, current), (second.Event, second.PreviousRevision, second.CredentialRevision, second.Fingerprint)));
    }

    /// <summary>Creates the actual OpenIddict confidential key registration and commits its approval before reading the issuer's public verification key.</summary>
    private static async Task<(string Id, JsonWebKey Key)> RegisterAsync(IServiceScopeFactory factory, NativeAssertionMaterial material, long now, CancellationToken token)
    {
        await using var scope = await ProtocolStorageScope.OpenAsync(factory, token);
        await using var transaction = await ProtocolStorageTransaction.OpenAsync(scope.Owner);
        var observed = DateTimeOffset.FromUnixTimeSeconds(now);
        var expires = observed.AddSeconds(CredentialSeconds).ToString(ApplicationEditorRules.ExpiryFormat, CultureInfo.InvariantCulture);
        var pub = material.Policy.Key.CreateNativeKey();
        var form = JsonSerializer.Serialize(new
        {
            kty = pub.Kty,
            kid = pub.Kid,
            alg = pub.Alg,
            use = pub.Use,
            n = pub.N,
            e = pub.E
        });
        var command = ApplicationMutationCommand.CreateWithKey(new ApplicationMachineCreate(Client, Name, expires), form, observed)!;
        await ManagedRegistration.LockAsync(scope, token);
        var state = await ApplicationMutationWriter.StageAsync(scope, command, token);
        var audit = await ApplicationMutationAudit.AuditAsync(scope, command, state, Actor, Guid.NewGuid(), token);
        await ApplicationMutationAudit.VerifyAuditAsync(scope, command, state, Actor, audit, token);
        await state.VerifyAsync(scope, token);
        var manager = scope.Services.GetRequiredService<IOpenIddictApplicationManager>();
        var app = await manager.FindByIdAsync(state.Id, token);
        var keys = await manager.GetJsonWebKeySetAsync(app!, token);
        var result = Assert.Single(keys!.Keys);
        Assert.Null(state.Credential(state.Details(observed.AddSeconds(CredentialSeconds))));
        await transaction.CommitAsync(token);
        return (state.Id, result);
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
