using System.Security.Claims;
using IdentityPlatform.AuthServer.Features.Protocol.ManagedApplications;
using IdentityPlatform.Infrastructure.Features.Applications;
using IdentityPlatform.Infrastructure.Features.Identity.ManagedApplications;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Persistence;
using IdentityPlatform.Persistence.Tests.Features.ProtocolStorage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace IdentityPlatform.Persistence.Tests.Features.ProviderCompatibility;

/// <summary>Exercises machine-independent native public registration, native code lookup and committed cadence observations on a real provider.</summary>
internal static class NativeDevicePollEvidence
{
    private const int NativePairSize = 2;
    private const int FirstSlowInterval = 10;
    internal const string Client = "television-one";
    private const string Name = "Television";
    internal static readonly Uri Issuer = new("https://devices.example.test/");
    private const string Devices = "/connect/device";
    internal const string Verification = "/connect/verify";
    internal const string Tokens = "/connect/token";
    private const int LifetimeSeconds = 600;
    private const string Scope = "openid";


    /// <summary>Uses the installed server's real native generators, public-key cryptography and provider-backed managers.</summary>
    internal static void Configure(IServiceCollection services)
    {
        services.AddOpenIddict().AddServer(server =>
        {
            server.SetIssuer(Issuer).SetDeviceAuthorizationEndpointUris(Devices).SetEndUserVerificationEndpointUris(Verification)
                .SetTokenEndpointUris(Tokens).AllowDeviceAuthorizationFlow().AddEphemeralEncryptionKey().AddEphemeralSigningKey()
                .SetDeviceCodeLifetime(TimeSpan.FromSeconds(LifetimeSeconds)).SetUserCodeLifetime(TimeSpan.FromSeconds(LifetimeSeconds));
            BoundDeviceApproval.Register(server);
            server.AddEventHandler<OpenIddictServerEvents.ApplyTokenResponseContext>(handler => handler.UseInlineHandler(response =>
            {
                response.HandleRequest();
                return ValueTask.CompletedTask;
            }));
            server.AddEventHandler<OpenIddictServerEvents.ApplyEndUserVerificationResponseContext>(handler => handler.UseInlineHandler(response =>
            {
                response.HandleRequest();
                return ValueTask.CompletedTask;
            }));
            server.AddEventHandler<OpenIddictServerEvents.ApplyDeviceAuthorizationResponseContext>(handler => handler.UseInlineHandler(response =>
            {
                response.HandleRequest();
                return ValueTask.CompletedTask;
            }));
        });
        services.AddOptions<OpenIddictServerOptions>().Configure<TimeProvider>((options, clock) => options.TimeProvider = clock);
    }

    /// <summary>Polls through the native reference ID, reopens a fresh database scope and observes the persisted slowdown.</summary>
    internal static async Task RequireAsync(ManagedSchemaFixture fixture)
    {
        var factory = fixture.Services.GetRequiredService<IServiceScopeFactory>();
        // A real issuer initializes its signing and encryption options at startup, before handling a device request.
        _ = fixture.Services.GetRequiredService<IOptionsMonitor<OpenIddictServerOptions>>().CurrentValue;
        var clock = fixture.Services.GetRequiredService<TimeProvider>();
        var id = await new ProtocolStorageRequest(clock, fixture.Token).RunAsync(factory, RegisterClientAsync);
        var (external, user) = await new ProtocolStorageRequest(clock, fixture.Token).RunAsync(factory, IssuePairAsync);
        await using (var polling = await ProtocolStorageScope.OpenAsync(factory, fixture.Token))
        {
            Assert.Equal(DevicePollResult.SlowDown, await DevicePollAdmission.PollAsync(polling, Client, external, fixture.Token));
        }
        await NativeDeviceVerificationEvidence.RequireDenialAsync(fixture, external, user);
        await using var scope = fixture.Scope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var policy = await database.Set<ApplicationPolicy>().SingleAsync(row => row.ApplicationId == id, fixture.Token);
        var persisted = await database.Set<DevicePollingWindow>().AsNoTracking().SingleAsync(row => row.ApplicationId == policy.ApplicationId, fixture.Token);
        Assert.Equal(FirstSlowInterval, persisted.IntervalSeconds);
        await NativeDeviceApprovalEvidence.RequireAsync(fixture, id);
        await RequireRetentionAsync(fixture, id);
        await database.Entry(policy).ReloadAsync(fixture.Token);
        policy.Enabled = false;
        policy.Revision++;
        await database.SaveChangesAsync(fixture.Token);
        await using var disabled = await ProtocolStorageScope.OpenAsync(factory, fixture.Token);
        Assert.Equal(DevicePollResult.Unavailable, await DevicePollAdmission.PollAsync(disabled, Client, external, fixture.Token));
    }

    /// <summary>Checks that a new, abandoned native pair survives its retention period, then releases the cadence, both tokens and its grant together.</summary>
    private static async Task RequireRetentionAsync(ManagedSchemaFixture fixture, string applicationId)
    {
        var factory = fixture.Services.GetRequiredService<IServiceScopeFactory>();
        var clock = Assert.IsType<NativeConsumptionClock>(fixture.Services.GetRequiredService<TimeProvider>());
        var start = clock.GetUtcNow().AddSeconds(LifetimeSeconds);
        clock.Set(start);
        var pair = await new ProtocolStorageRequest(clock, fixture.Token).RunAsync(factory, async (scope, token) =>
        {
            await using var tx = await ProtocolStorageTransaction.OpenAsync(scope.Owner);
            var issued = await CreatePairAsync(scope, token);
            var manager = scope.Services.GetRequiredService<IOpenIddictTokenManager>();
            var entry = await manager.FindByReferenceIdAsync(issued.Code, token);
            var id = (await manager.GetIdAsync(entry!, token))!;
            var authorization = (await manager.GetAuthorizationIdAsync(entry!, token))!;
            await tx.CommitAsync(token);
            return (Device: id, Grant: authorization);
        });
        foreach (var retained in new[] { true, false })
        {
            clock.Set(start.AddDays(ManagedLoginRules.RetentionDays + (retained ? 0 : 1)));
            if (!retained)
            {
                await SeedHeldPageAsync(fixture, applicationId, pair.Grant, start.ToUnixTimeSeconds());
                await SweepAsync(fixture, clock);
            }
            await SweepAsync(fixture, clock);
            await new ProtocolStorageRequest(clock, fixture.Token).RunAsync(factory, async (scope, token) =>
            {
                var manager = scope.Services.GetRequiredService<IOpenIddictTokenManager>();
                var grants = scope.Services.GetRequiredService<IOpenIddictAuthorizationManager>();
                Assert.Equal(retained, await grants.FindByIdAsync(pair.Grant, token) is not null);
                Assert.Equal(retained ? NativePairSize : 0, await manager.FindByAuthorizationIdAsync(pair.Grant, token).CountAsync(token));
                var digest = ManagedLoginRules.Digest(pair.Device);
                Assert.Equal(retained, await scope.Owner.RunAsync(current => new ValueTask<bool>(scope.Owner.Database.Set<DevicePollingWindow>().AnyAsync(window => window.Digest == digest, current)), token));
                return true;
            });
        }
    }

    /// <summary>Places a full older page of mismatched windows ahead of a genuine retained grant to verify progress across held records.</summary>
    private static async Task SeedHeldPageAsync(ManagedSchemaFixture fixture, string applicationId, string authorization, long start)
    {
        await using var service = fixture.Scope();
        var database = service.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var policy = new ApplicationPolicy { ApplicationId = applicationId, Profile = DevicePollingWindow.Profile, Revision = 1 };
        for (var index = 0; index < ManagedLoginRules.CleanupLimit; index++)
        {
            var held = DevicePollingWindow.Start(ManagedSchemaRows.Digest(), policy, start - LifetimeSeconds);
            held.NativeAuthorizationId = authorization;
            database.Add(held);
        }
        await database.SaveChangesAsync(fixture.Token);
    }

    /// <summary>Runs one normal bounded maintenance epoch with independently committed progress.</summary>
    private static Task<bool> SweepAsync(ManagedSchemaFixture fixture, TimeProvider clock) =>
        new ProtocolStorageRequest(clock, fixture.Token).RunAsync(fixture.Services.GetRequiredService<IServiceScopeFactory>(), async (scope, token) =>
        {
            await using var tx = await ProtocolStorageTransaction.OpenAsync(scope.Owner);
            await ManagedRegistration.LockAsync(scope, token);
            await DeviceGrantCleanup.RunAsync(scope, clock.GetUtcNow().ToUnixTimeSeconds(), TimeSpan.Zero, token);
            await tx.CommitAsync(token);
            return true;
        });

    /// <summary>Commits the public application and its policy before a device can initiate an independent request.</summary>
    private static async ValueTask<string> RegisterClientAsync(ProtocolStorageScope scope, CancellationToken token)
    {
        await using var transaction = await ProtocolStorageTransaction.OpenAsync(scope.Owner);
        var apps = scope.Services.GetRequiredService<IOpenIddictApplicationManager>();
        var app = await apps.CreateAsync(DeviceClientRegistration.Descriptor(Client, Name), token);
        var id = (await apps.GetIdAsync(app, token))!;
        var policy = new ApplicationPolicy { ApplicationId = id, Profile = DevicePollingWindow.Profile, Revision = 1 };
        await scope.Owner.RunAsync(async current =>
        {
            scope.Owner.Database.Add(policy);
            await scope.Owner.Database.SaveChangesAsync(current);
        }, token, write: true);
        await transaction.CommitAsync(token);
        return id;
    }

    /// <summary>Issues a native code pair and records its cadence within the original request and transaction deadlines.</summary>
    internal static async ValueTask<(string Code, string User)> IssuePairAsync(ProtocolStorageScope scope, CancellationToken token)
    {
        await using var transaction = await ProtocolStorageTransaction.OpenAsync(scope.Owner);
        var pair = await CreatePairAsync(scope, token);
        await transaction.CommitAsync(token);
        return pair;
    }

    /// <summary>Uses the original generated principal to bind both native code entries; the client receives the native opaque reference.</summary>
    internal static async Task<(string Code, string User)> CreatePairAsync(ProtocolStorageScope scope, CancellationToken token)
    {
        var native = await scope.Services.GetRequiredService<IOpenIddictServerFactory>().CreateTransactionAsync();
        native.EndpointType = OpenIddictServerEndpointType.DeviceAuthorization;
        native.RequestUri = new Uri(Issuer, Devices);
        native.BaseUri = Issuer;
        native.Request = new OpenIddictRequest { ClientId = Client, Scope = Scope };
        native.Response = new OpenIddictResponse();
        var signIn = new OpenIddictServerEvents.ProcessSignInContext(native)
        {
            Principal = new ClaimsPrincipal(new ClaimsIdentity()).SetScopes(Scope),
            CancellationToken = token
        };
        await scope.Services.GetRequiredService<IOpenIddictServerDispatcher>().DispatchAsync(signIn);
        Assert.True(signIn.IsRequestHandled);
        Assert.False(signIn.IsRejected);
        Assert.NotNull(signIn.Response.UserCode);
        Assert.NotNull(signIn.Response.DeviceCode);
        Assert.NotEqual(signIn.Response.UserCode, signIn.Response.DeviceCode);
        Assert.Equal(LifetimeSeconds, signIn.Response.ExpiresIn);
        Assert.Equal(new Uri(Issuer, Verification).AbsoluteUri, signIn.Response.VerificationUri);
        await DevicePollAdmission.RegisterLockedAsync(scope, native.Request, signIn.UserCodePrincipal!, token);
        return (signIn.Response.DeviceCode, signIn.Response.UserCode);
    }
}
