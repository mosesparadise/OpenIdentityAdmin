using System.Security.Claims;
using IdentityPlatform.AuthServer.Features.Protocol.ManagedApplications;
using IdentityPlatform.Infrastructure.Features.Applications;
using IdentityPlatform.Infrastructure.Features.Identity.ManagedApplications;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using OpenIddict.Server.AspNetCore;

namespace IdentityPlatform.AuthServer.Features.Protocol.Devices;

/// <summary>Runs native device issuance and exchange under current registration authority in the same transaction as their durable effects.</summary>
internal static class DeviceProtocol
{
    private const int PairTokens = 2;

    /// <summary>Validates the public registration and requested scope, then stores the original native code pair and cadence atomically.</summary>
    internal static async ValueTask AuthorizeAsync(ProtocolStorageScope scope, DeviceNative native, CancellationToken token)
    {
        var request = native.Transaction.Request!;
        var dispatcher = scope.Services.GetRequiredService<IOpenIddictServerDispatcher>();
        var validation = new OpenIddictServerEvents.ValidateDeviceAuthorizationRequestContext(native.Transaction) { CancellationToken = token };
        await dispatcher.DispatchAsync(validation);
        if (validation.IsRejected)
        {
            await ErrorAsync(scope, native, validation.Error ?? OpenIddictConstants.Errors.InvalidRequest, token);
            return;
        }
        if (validation.IsRequestHandled || validation.IsRequestSkipped)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        var scopes = ManagedRequestedScopes.Read(request.Scope);
        var policy = scopes is null ? null : await ManagedScopePolicy.TryCaptureAsync(scope, request.ClientId!, scopes.Value, scopes.Value, token, device: true);
        if (policy is null)
        {
            await ErrorAsync(scope, native, OpenIddictConstants.Errors.InvalidScope, token);
            return;
        }
        var now = scope.Owner.Clock.GetUtcNow().ToUnixTimeSeconds();
        var budget = await ManagedNativeBudget.ReadAsync(scope, now, token);
        if (budget.Tokens + PairTokens > ManagedLoginRules.TokenLimit || budget.Authorizations >= ManagedLoginRules.AuthorizationLimit)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        native.RequireInput();
        var issued = await native.SignInAsync(new ClaimsPrincipal(new ClaimsIdentity()).SetScopes(policy.Requested), token);
        if (issued.UserCodePrincipal is not { } user || string.IsNullOrEmpty(issued.Response.DeviceCode) || string.IsNullOrEmpty(issued.Response.UserCode) ||
            issued.Response.ExpiresIn is not (> 0 and <= DevicePollingWindow.LifetimeSeconds) || issued.Response.VerificationUri != new Uri(native.Transaction.BaseUri!, DeviceNative.VerificationPath).AbsoluteUri)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        await RequirePairAsync(scope, native, issued, user, token);
        await DevicePollAdmission.RegisterLockedAsync(scope, request, user, token);
        var final = await ManagedNativeBudget.ReadAsync(scope, now, token);
        if (final.Tokens != budget.Tokens + PairTokens || final.Authorizations != budget.Authorizations + 1 || final.Requests != budget.Requests || final.LiveRequests != budget.LiveRequests)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        ProtocolStorageAmbient.Current!.Request.RestrictPublication(user.GetExpirationDate() ?? throw ProtocolStorageOwner.Unavailable());
    }

    /// <summary>Resolves both emitted references with the library and checks they identify the actual persisted native pair.</summary>
    private static async ValueTask RequirePairAsync(ProtocolStorageScope scope, DeviceNative native, OpenIddictServerEvents.ProcessSignInContext issued, ClaimsPrincipal user, CancellationToken token)
    {
        var verify = await DeviceNative.CreateAsync(scope, OpenIddictServerEndpointType.EndUserVerification, new()
        {
            UserCode = issued.Response.UserCode
        });
        var human = await verify.UserAsync(token);
        var manager = scope.Services.GetRequiredService<IOpenIddictTokenManager>();
        var device = await manager.FindByReferenceIdAsync(issued.Response.DeviceCode!, token);
        if (device is null || human is null || issued.Response.UserCode != issued.UserCode || issued.Response.DeviceCode != issued.DeviceCode ||
            human.GetTokenId() != user.GetTokenId() || human.GetAuthorizationId() != user.GetAuthorizationId() ||
            await manager.GetIdAsync(device, token) != user.GetClaim(OpenIddictConstants.Claims.Private.DeviceCodeId))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        native.RequireInput();
    }

    /// <summary>Checks native authentication, the persisted human decision, registration and current account before one native redemption.</summary>
    internal static async ValueTask ExchangeAsync(ProtocolStorageScope scope, DeviceNative native, DevicePollReceipt cadence, CancellationToken token)
    {
        if (cadence.Result != DevicePollResult.Admitted)
        {
            await ErrorAsync(scope, native, cadence.Result switch
            {
                DevicePollResult.SlowDown => OpenIddictConstants.Errors.SlowDown,
                DevicePollResult.Expired => OpenIddictConstants.Errors.ExpiredToken,
                _ => OpenIddictConstants.Errors.InvalidGrant
            }, token);
            return;
        }
        await ManagedRegistration.LockAsync(scope, token);
        var validation = new OpenIddictServerEvents.ValidateTokenRequestContext(native.Transaction) { CancellationToken = token };
        await scope.Services.GetRequiredService<IOpenIddictServerDispatcher>().DispatchAsync(validation);
        if (validation.IsRejected)
        {
            await ErrorAsync(scope, native, validation.Error ?? OpenIddictConstants.Errors.InvalidGrant, token);
            return;
        }
        if (validation.IsRequestHandled || validation.IsRequestSkipped || validation.DeviceCodePrincipal is not { } device)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        native.RequireInput();
        var approval = await ApprovedAsync(scope, native.Transaction.Request!.ClientId!, device, token);
        if (approval is null || approval.Row.DeviceEntryId != cadence.EntryId || approval.Row.ApplicationId != cadence.ApplicationId || approval.Row.ApplicationRevision != cadence.ApplicationRevision)
        {
            await ErrorAsync(scope, native, OpenIddictConstants.Errors.InvalidGrant, token);
            return;
        }
        await IssueTokensAsync(scope, native, approval, token);
    }

    /// <summary>Issues only the two consented token types and rechecks persisted source, claim destinations and storage budgets after native formatting.</summary>
    private static async ValueTask IssueTokensAsync(ProtocolStorageScope scope, DeviceNative native, Approval approval, CancellationToken token)
    {
        var budget = await ManagedNativeBudget.ReadAsync(scope, scope.Owner.Clock.GetUtcNow().ToUnixTimeSeconds(), token);
        var (source, row, snapshot, email) = approval;
        var tokens = source.HasScope(OpenIddictConstants.Scopes.OpenId) ? PairTokens : 1;
        if (budget.Tokens + tokens > ManagedLoginRules.TokenLimit)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        var issued = await native.SignInAsync(source, token);
        if (string.IsNullOrEmpty(issued.Response.AccessToken) || issued.Response.RefreshToken is not null || issued.Response.AccessToken != issued.AccessToken ||
            string.IsNullOrEmpty(issued.IdentityToken) || issued.Response.IdToken != issued.IdentityToken ||
            issued.AccessTokenPrincipal?.GetClaim(OpenIddictConstants.Claims.Subject) != source.GetClaim(OpenIddictConstants.Claims.Subject) ||
            !issued.AccessTokenPrincipal!.GetAudiences().ToHashSet(StringComparer.Ordinal).SetEquals(source.GetResources()) ||
            issued.IdentityTokenPrincipal is not { } identity || identity.GetClaim(OpenIddictConstants.Claims.Subject) != row.UserId ||
            !ManagedEmailClaims.Matches(issued.AccessTokenPrincipal!, null, destinations: false) || !ManagedEmailClaims.Matches(identity, email, destinations: false))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        await DeviceGrantState.IssuanceAsync(scope, row, issued, token);
        var finalBudget = await ManagedNativeBudget.ReadAsync(scope, scope.Owner.Clock.GetUtcNow().ToUnixTimeSeconds(), token);
        var expectedBudget = budget with
        {
            Tokens = budget.Tokens + tokens
        };
        await scope.Owner.RunAsync(current => new ValueTask(snapshot.VerifyAsync((PlatformDbContext)scope.Owner.Database, scope.Owner.Clock, current, completedDevice: true)), token);
        await ManagedScopeBinding.RequireAsync(scope, row, native.Transaction.Request!.ClientId!, token);
        if (expectedBudget != finalBudget)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
    }

    /// <summary>Reconstructs current subject and granted resources from the original confirmed browser interaction.</summary>
    private static async ValueTask<Approval?> ApprovedAsync(ProtocolStorageScope scope, string client, ClaimsPrincipal device, CancellationToken token)
    {
        if (device.GetTokenId() is not { } id)
        {
            return null;
        }
        var row = await scope.Owner.RunAsync(current => new ValueTask<ManagedLoginInteraction?>(scope.Owner.Database.Set<ManagedLoginInteraction>().AsNoTracking()
            .SingleOrDefaultAsync(value => value.DeviceEntryId == id, current)), token);
        var registration = await DeviceClientRegistration.ReadLockedAsync(scope, client, token);
        if (row is not { State: ManagedLoginState.DeviceApproved, UserId: not null } || registration is null ||
            row.ApplicationId != registration.Value.Policy.ApplicationId || row.ApplicationRevision != registration.Value.Policy.Revision ||
            row.AuthorizationId is null || row.AuthorizationId != device.GetAuthorizationId() || row.UserId != device.GetClaim(OpenIddictConstants.Claims.Subject) || device.GetPresenters() is not [var presenter] || presenter != client ||
            device.GetExpirationDate() is not { } expiry || expiry.ToUnixTimeSeconds() != row.NativeDependencyExpiresAt ||
            expiry <= scope.Owner.Clock.GetUtcNow() || await ManagedIssuanceSource.TryOpenAsync(scope, row, observe: false, token, completedDevice: true) is not { } source)
        {
            return null;
        }
        await ManagedScopeBinding.RequireAsync(scope, row, client, token);
        var decision = ManagedScopeBinding.Read(row);
        if (!decision.Granted.ToHashSet(StringComparer.Ordinal).SetEquals(device.GetScopes()) || !decision.Resources.ToHashSet(StringComparer.Ordinal).SetEquals(device.GetResources()))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        ProtocolStorageAmbient.Current!.Request.RestrictPublication(expiry);
        var principal = new ClaimsPrincipal(new ClaimsIdentity(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme)).SetClaim(OpenIddictConstants.Claims.Subject, row.UserId)
            .SetScopes(decision.Granted).SetPresenters(client).SetResources(decision.Resources);
        var email = source.Email(row);
        ManagedEmailClaims.Attach(principal, email);
        return new(principal, row, source.Snapshot(row), email);
    }

    private sealed record Approval(ClaimsPrincipal Principal, ManagedLoginInteraction Row, ManagedHumanSnapshot Snapshot, ManagedEmailProjection? Email);

    /// <summary>Lets the installed issuer normalize OAuth errors and format only a token-free response.</summary>
    internal static async ValueTask ErrorAsync(ProtocolStorageScope scope, DeviceNative native, string error, CancellationToken token)
    {
        var refusal = new OpenIddictServerEvents.ProcessErrorContext(native.Transaction) { CancellationToken = token, Error = error };
        await scope.Services.GetRequiredService<IOpenIddictServerDispatcher>().DispatchAsync(refusal);
        var response = native.Transaction.Response;
        var expected = native.Transaction.Request!.IsDeviceCodeGrantType() && error == OpenIddictConstants.Errors.InvalidToken ? OpenIddictConstants.Errors.InvalidGrant : error;
        if (!refusal.IsRequestHandled || response is null || response.Error != expected || response.AccessToken is not null ||
            response.DeviceCode is not null || response.UserCode is not null)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
    }
}
