using System.Security.Claims;
using IdentityPlatform.Infrastructure.Features.Applications;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Persistence;
using IdentityPlatform.Persistence.Tests.Features.ProtocolStorage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace IdentityPlatform.Persistence.Tests.Features.ProviderCompatibility;

/// <summary>Uses the installed verification and redemption handlers after an explicit synthetic account decision.</summary>
internal static class NativeDeviceApprovalEvidence
{
    private const string Subject = "verified-device-account";
    private const string Scheme = "AuthenticatedDeviceAccount";
    private const int ApproveAfter = 30;
    private const int ObserveAfter = 25;
    private const int RedeemAfter = 40;
    private const int RetryAfter = 50;
    private const int OriginalLifetime = 600;

    /// <summary>Approves an actual user code, preserves its first deadline, exchanges the device code once and refuses its reuse.</summary>
    internal static async Task RequireAsync(ManagedSchemaFixture fixture, string applicationId)
    {
        var clock = Assert.IsType<NativeConsumptionClock>(fixture.Services.GetRequiredService<TimeProvider>());
        var start = clock.GetUtcNow();
        var factory = fixture.Services.GetRequiredService<IServiceScopeFactory>();
        var first = await new ProtocolStorageRequest(clock, fixture.Token).RunAsync(factory, NativeDevicePollEvidence.IssuePairAsync);
        var second = await new ProtocolStorageRequest(clock, fixture.Token).RunAsync(factory, NativeDevicePollEvidence.IssuePairAsync);
        await using (var observation = fixture.Scope())
        {
            var db = observation.ServiceProvider.GetRequiredService<PlatformDbContext>();
            Assert.Equal(1, await db.Set<ApplicationPolicy>().Where(row => row.ApplicationId == applicationId)
                .ExecuteUpdateAsync(update => update.SetProperty(row => row.ObservedAt, start.AddSeconds(ObserveAfter).ToUnixTimeSeconds()), fixture.Token));
        }
        clock.Set(start.AddSeconds(ApproveAfter));
        var deadline = start.AddSeconds(OriginalLifetime);
        foreach (var pair in new[] { first, second })
        {
            var binding = await new ProtocolStorageRequest(clock, fixture.Token).RunAsync(factory, (scope, token) => ApproveAsync(scope, pair.User, deadline, token));
            await RequirePersistedAsync(fixture, pair.Code, binding, applicationId, deadline);
        }
        clock.Set(start.AddSeconds(RedeemAfter));
        await RedeemAsync(fixture, first.Code, used: false);
        clock.Set(start.AddSeconds(RetryAfter));
        await RedeemAsync(fixture, first.Code, used: true);
        clock.Set(deadline);
        var expired = await new ProtocolStorageRequest(clock, fixture.Token).RunAsync(factory,
            (scope, token) => DevicePollAdmission.PollAsync(scope, NativeDevicePollEvidence.Client, second.Code, token));
        Assert.Equal(DevicePollResult.Expired, expired);
        clock.Set(start);
    }

    /// <summary>Reopens the committed native entries independently and checks the subject, registration, authorization, deadline and used human code.</summary>
    private static Task<bool> RequirePersistedAsync(ManagedSchemaFixture fixture, string device, ApprovedPair binding, string applicationId, DateTimeOffset deadline)
    {
        var request = new ProtocolStorageRequest(fixture.Services.GetRequiredService<TimeProvider>(), fixture.Token);
        return request.RunAsync(fixture.Services.GetRequiredService<IServiceScopeFactory>(), async (scope, token) =>
        {
            var manager = scope.Services.GetRequiredService<IOpenIddictTokenManager>();
            var approved = await manager.FindByReferenceIdAsync(device, token);
            Assert.NotNull(approved);
            Assert.Equal(Subject, await manager.GetSubjectAsync(approved, token));
            Assert.Equal(applicationId, await manager.GetApplicationIdAsync(approved, token));
            Assert.Equal(deadline, await manager.GetExpirationDateAsync(approved, token));
            Assert.True(await manager.HasStatusAsync(approved, OpenIddictConstants.Statuses.Valid, token));
            Assert.Equal(binding.DeviceId, await manager.GetIdAsync(approved, token));
            var human = await manager.FindByIdAsync(binding.UserId, token);
            Assert.NotNull(human);
            Assert.True(await manager.HasStatusAsync(human, OpenIddictConstants.Statuses.Redeemed, token));
            var authorizationId = await manager.GetAuthorizationIdAsync(human, token);
            Assert.Equal(binding.AuthorizationId, authorizationId);
            Assert.Equal(authorizationId, await manager.GetAuthorizationIdAsync(approved, token));
            return true;
        });
    }

    private sealed record ApprovedPair(string DeviceId, string UserId, string AuthorizationId);

    /// <summary>Uses a new verification request and the native user-code authentication from that request to approve its original scopes.</summary>
    private static async ValueTask<ApprovedPair> ApproveAsync(ProtocolStorageScope scope, string user, DateTimeOffset deadline, CancellationToken token)
    {
        await using var transaction = await ProtocolStorageTransaction.OpenAsync(scope.Owner);
        var native = await NativeDeviceVerificationEvidence.CreateAsync(scope, OpenIddictServerEndpointType.EndUserVerification, new()
        {
            UserCode = user
        });
        var auth = await AuthenticateAsync(scope, native, token);
        Assert.False(auth.IsRejected);
        var original = Assert.IsType<ClaimsPrincipal>(auth.UserCodePrincipal);
        var principal = new ClaimsPrincipal(new ClaimsIdentity(Scheme)).SetClaim(OpenIddictConstants.Claims.Subject, Subject).SetScopes(original.GetScopes());
        var result = new OpenIddictServerEvents.ProcessSignInContext(native) { Principal = principal, CancellationToken = token };
        await scope.Services.GetRequiredService<IOpenIddictServerDispatcher>().DispatchAsync(result);
        Assert.True(result.IsRequestHandled);
        Assert.Equal(deadline, result.DeviceCodePrincipal!.GetExpirationDate());
        var deviceId = Assert.IsType<string>(original.GetClaim(OpenIddictConstants.Claims.Private.DeviceCodeId));
        var userId = Assert.IsType<string>(original.GetTokenId());
        var authorizationId = Assert.IsType<string>(original.GetAuthorizationId());
        await transaction.CommitAsync(token);
        return new(deviceId, userId, authorizationId);
    }

    /// <summary>Admits the actual next poll in its own committed epoch, then independently checks the native token response or refusal.</summary>
    private static async Task RedeemAsync(ManagedSchemaFixture fixture, string device, bool used)
    {
        var factory = fixture.Services.GetRequiredService<IServiceScopeFactory>();
        var request = new ProtocolStorageRequest(fixture.Services.GetRequiredService<TimeProvider>(), fixture.Token);
        var cadence = await request.RunAsync(factory, (scope, token) => DevicePollAdmission.PollAsync(scope, NativeDevicePollEvidence.Client, device, token));
        Assert.Equal(DevicePollResult.Admitted, cadence);
        await request.RunAsync(factory, async (scope, token) =>
        {
            await using var transaction = await ProtocolStorageTransaction.OpenAsync(scope.Owner);
            var native = await NativeDeviceVerificationEvidence.CreateAsync(scope, OpenIddictServerEndpointType.Token, new()
            {
                ClientId = NativeDevicePollEvidence.Client,
                GrantType = OpenIddictConstants.GrantTypes.DeviceCode,
                DeviceCode = device
            });
            var auth = await AuthenticateAsync(scope, native, token);
            if (used)
            {
                Assert.True(auth.IsRejected);
                Assert.Equal(OpenIddictConstants.Errors.InvalidToken, auth.Error);
                var refusal = new OpenIddictServerEvents.ProcessErrorContext(native) { Error = auth.Error, CancellationToken = token };
                await scope.Services.GetRequiredService<IOpenIddictServerDispatcher>().DispatchAsync(refusal);
                Assert.True(refusal.IsRequestHandled);
                Assert.Equal(OpenIddictConstants.Errors.InvalidGrant, native.Response!.Error);
                Assert.Null(native.Response.AccessToken);
            }
            else
            {
                Assert.False(auth.IsRejected);
                var principal = Assert.IsType<ClaimsPrincipal>(auth.DeviceCodePrincipal);
                Assert.Equal(Subject, principal.GetClaim(OpenIddictConstants.Claims.Subject));
                var issue = new OpenIddictServerEvents.ProcessSignInContext(native) { Principal = principal, CancellationToken = token };
                await scope.Services.GetRequiredService<IOpenIddictServerDispatcher>().DispatchAsync(issue);
                Assert.True(issue.IsRequestHandled);
                Assert.False(string.IsNullOrEmpty(issue.Response.AccessToken));
                Assert.Equal(Subject, issue.AccessTokenPrincipal!.GetClaim(OpenIddictConstants.Claims.Subject));
            }
            await transaction.CommitAsync(token);
            return true;
        });
    }

    /// <summary>Runs native user- or device-code authentication and retains its result on the exact corresponding transaction.</summary>
    private static async ValueTask<OpenIddictServerEvents.ProcessAuthenticationContext> AuthenticateAsync(ProtocolStorageScope scope, OpenIddictServerTransaction native, CancellationToken token)
    {
        var authentication = new OpenIddictServerEvents.ProcessAuthenticationContext(native) { CancellationToken = token };
        await scope.Services.GetRequiredService<IOpenIddictServerDispatcher>().DispatchAsync(authentication);
        native.SetProperty(typeof(OpenIddictServerEvents.ProcessAuthenticationContext).FullName!, authentication);
        return authentication;
    }
}
