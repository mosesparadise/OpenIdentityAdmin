using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using IdentityPlatform.Infrastructure.Features.Applications;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Infrastructure.Features.Identity.ManagedApplications;

/// <summary>Binds the shared human ceremony to a pending native user and device code without a redirect or an authorization-code request.</summary>
internal static class ManagedDeviceInteraction
{
    /// <summary>Starts one browser-bound interaction for a live native pair. The human deadline can only shorten the original device window.</summary>
    internal static async ValueTask<ManagedHumanRequest?> StartAsync(ProtocolStorageScope scope, ClaimsPrincipal user, string code, string browser, TimeSpan skew, CancellationToken token)
    {
        var client = user.GetClaim(OpenIddictConstants.Claims.ClientId) ?? throw ProtocolStorageOwner.Unavailable();
        var deadline = await DevicePollAdmission.ApprovalDeadlineLockedAsync(scope, user, user.GetScopes(), token, redeemed: false);
        var registration = await DeviceClientRegistration.ReadLockedAsync(scope, client, token) ?? throw ProtocolStorageOwner.Unavailable();
        var now = ManagedLoginRules.Observe(scope.Owner.Clock, registration.Policy.ObservedAt);
        var binding = Binding(user, code, browser, registration.Policy, deadline.ToUnixTimeSeconds(), now, skew);
        var selector = Convert.ToHexString(BrowserProof(browser, nameof(ManagedHumanRequest.Selector), binding.RequestId!));
        binding.SelectorDigest = ManagedLoginRules.Digest(selector);
        var decision = await ManagedScopePolicy.CaptureAsync(scope, client, user.GetScopes(), user.GetScopes(), token, device: true);
        ManagedScopeBinding.Attach(binding, decision);
        await ManagedLoginCleanup.RunAsync(scope, now, skew, token);
        var prior = await scope.Owner.RunAsync(current => new ValueTask<ManagedLoginInteraction?>(scope.Owner.Database.Set<ManagedLoginInteraction>()
            .AsNoTracking().SingleOrDefaultAsync(row => row.RequestId == binding.RequestId, current)), token);
        if (prior is not null)
        {
            if (prior.SelectorDigest != binding.SelectorDigest || prior.BrowserDigest != binding.BrowserDigest || prior.DemandDigest != binding.DemandDigest ||
                prior.PolicyDigest != binding.PolicyDigest || prior.ApplicationRevision != binding.ApplicationRevision || prior.ExpiresAt <= now ||
                prior.ObservedAt > now || prior.State is not (ManagedLoginState.PendingPassword or ManagedLoginState.PendingMfa or ManagedLoginState.PendingConsent))
            {
                return null;
            }
            binding = prior;
        }
        else
        {
            if (!await ManagedLoginReservation.HasCapacityAsync(scope, binding.BrowserDigest, now, token))
            {
                return null;
            }
            await scope.Owner.RunAsync(async current =>
            {
                scope.Owner.Database.Add(binding);
                ManagedScopeReferences.Add(scope.Owner.Database, binding, decision);
                await scope.Owner.Database.SaveChangesAsync(current);
            }, token, write: true);
        }
        return Form(binding, selector, browser, code, client, registration.DisplayName);
    }

    /// <summary>Creates the original browser deadline and permanent link to the existing native code pair.</summary>
    private static ManagedLoginInteraction Binding(ClaimsPrincipal user, string code, string browser, ApplicationPolicy policy, long deadline, long now, TimeSpan skew)
    {
        var expires = Math.Min(now + ManagedLoginRules.LifetimeSeconds, deadline);
        return new()
        {
            BrowserDigest = ManagedLoginRules.Digest(browser),
            ApplicationId = policy.ApplicationId,
            ApplicationRevision = policy.Revision,
            AuthorizationId = user.GetAuthorizationId() ?? throw ProtocolStorageOwner.Unavailable(),
            RequestId = user.GetTokenId() ?? throw ProtocolStorageOwner.Unavailable(),
            DeviceEntryId = user.GetClaim(OpenIddictConstants.Claims.Private.DeviceCodeId) ?? throw ProtocolStorageOwner.Unavailable(),
            DemandDigest = Demand(user, code),
            CreatedAt = now,
            ObservedAt = now,
            Revision = 1,
            CorrelationId = Guid.NewGuid(),
            State = ManagedLoginState.PendingPassword,
            ExpiresAt = expires,
            RequestExpiresAt = expires,
            NativeDependencyCreatedAt = user.GetCreationDate()?.ToUnixTimeSeconds() ?? throw ProtocolStorageOwner.Unavailable(),
            NativeDependencyExpiresAt = deadline,
            RetainUntil = ManagedLoginRules.RetainUntil(now, deadline, skew)
        };
    }

    /// <summary>Recreates the same protected form intent after an interrupted submission from the original browser.</summary>
    private static ManagedHumanRequest Form(ManagedLoginInteraction row, string selector, string browser, string code, string client, string display) => new()
    {
        Selector = selector,
        BrowserDigest = row.BrowserDigest,
        ClientId = client,
        DeviceUserCode = code,
        DeviceDisplayName = display,
        DemandDigest = row.DemandDigest,
        RequestId = row.RequestId!,
        PolicyDigest = row.PolicyDigest,
        Correlation = WebEncoders.Base64UrlEncode(BrowserProof(browser, nameof(ManagedHumanRequest.Correlation), row.RequestId!)),
        State = row.State,
        Revision = row.Revision,
        ExpiresAt = row.ExpiresAt
    };

    /// <summary>Derives independent selector and ceremony secrets from the private browser cookie and server-assigned native id.</summary>
    private static byte[] BrowserProof(string browser, string purpose, string id) =>
        HMACSHA256.HashData(Convert.FromHexString(browser), Encoding.UTF8.GetBytes(purpose + '.' + id));

    /// <summary>Rechecks both native anonymous entries, the current policy revision and the original registered polling window at each human phase.</summary>
    internal static async ValueTask<ManagedRegistration> ReadAsync(ProtocolStorageScope scope, ManagedHumanRequest request, ManagedLoginInteraction row, CancellationToken token)
    {
        var registration = await DeviceClientRegistration.ReadLockedAsync(scope, request.ClientId, token) ?? throw ProtocolStorageOwner.Unavailable();
        if (request.DeviceUserCode is null || request.Callback.Length != 0 || request.RequestUri.Length != 0 ||
            request.DeviceDisplayName != registration.DisplayName || row.ApplicationId != registration.Policy.ApplicationId || row.ApplicationRevision != registration.Policy.Revision || row.DeviceEntryId is null)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        var manager = scope.Services.GetRequiredService<IOpenIddictTokenManager>();
        var user = await manager.FindByIdAsync(request.RequestId, token) ?? throw ProtocolStorageOwner.Unavailable();
        var authorization = await manager.GetAuthorizationIdAsync(user, token) ?? throw ProtocolStorageOwner.Unavailable();
        var principal = new ClaimsPrincipal(new ClaimsIdentity()).SetClaim(OpenIddictConstants.Claims.ClientId, request.ClientId)
            .SetTokenId(request.RequestId).SetClaim(OpenIddictConstants.Claims.Private.DeviceCodeId, row.DeviceEntryId)
            .SetAuthorizationId(authorization).SetPresenters(request.ClientId).SetScopes(ManagedScopeBinding.Read(row).Requested);
        if (authorization != row.AuthorizationId || Demand(principal, request.DeviceUserCode) != row.DemandDigest ||
            (await DevicePollAdmission.ApprovalDeadlineLockedAsync(scope, principal, principal.GetScopes(), token, redeemed: false)).ToUnixTimeSeconds() != row.NativeDependencyExpiresAt)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        return registration.State.Registration;
    }

    /// <summary>Commits the stable native pair, client, exact scopes and human reference to the browser's protected request.</summary>
    internal static string Demand(ClaimsPrincipal user, string code) => FactorBinding.Digest(new
    {
        Client = user.GetClaim(OpenIddictConstants.Claims.ClientId),
        User = user.GetTokenId(),
        Device = user.GetClaim(OpenIddictConstants.Claims.Private.DeviceCodeId),
        Authorization = user.GetAuthorizationId(),
        Scopes = user.GetScopes().Order(StringComparer.Ordinal).ToArray(),
        Code = ManagedLoginRules.Digest(code)
    });
}
