using System.Security.Claims;
using IdentityPlatform.AuthServer.Features.Protocol.ManagedApplications;
using IdentityPlatform.Infrastructure.Features.Applications;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace IdentityPlatform.Persistence.Tests.Features.ProviderCompatibility;

/// <summary>Exercises machine-independent native public registration, native code lookup and committed cadence observations on a real provider.</summary>
internal static class NativeDevicePollEvidence
{
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
        await database.Entry(policy).ReloadAsync(fixture.Token);
        policy.Enabled = false;
        policy.Revision++;
        await database.SaveChangesAsync(fixture.Token);
        await using var disabled = await ProtocolStorageScope.OpenAsync(factory, fixture.Token);
        Assert.Equal(DevicePollResult.Unavailable, await DevicePollAdmission.PollAsync(disabled, Client, external, fixture.Token));
    }

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
