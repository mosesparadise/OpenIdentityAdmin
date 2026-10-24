using System.Security.Claims;
using IdentityPlatform.AuthServer.Features.Protocol.ClientAssertions;
using IdentityPlatform.AuthServer.Features.Protocol.Ownership;
using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Features.Identity.ProtocolAdmission;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace IdentityPlatform.AuthServer.Features.Protocol.Administrator;

/// <summary>Combines original native consumption with fresh locked account authority for one code or token issuance epoch.</summary>
internal sealed class AdministratorAccountAuthority(ProtocolStorageScope scope, OpenIddictServerEvents.ProcessSignInContext context)
{
    private AdministratorDecision? decision;
    private DurableServerConsumption? consumption;
    private DurableClientAssertion? assertion;
    private ProtocolAdmissionProof? admission;
    private ProtocolAdmissionClaims? claims;
    private ClaimsPrincipal? projected;
    private string? publicClaims;
    private long revision;
    private BrowserIssuanceAuthority? browser;
    internal AdministratorRegistrationBinding? Registration
    {
        get; private set;
    }

    /// <summary>Acquires governance/profile/account locks before any tracked read, then claims native receipts and reads live source authority.</summary>
    internal async ValueTask OpenAsync(AdministratorRegistrationPolicy policy, CancellationToken token)
    {
        decision = AdministratorDecision.Claim(context);
        var ownership = await ProtocolAdmissionOwnership.AcquireAsync(scope.Owner, decision.UserId, token);
        Registration = await AdministratorRegistration.ReadLockedAsync(scope, policy, token);
        consumption = context.Transaction.GetProperty<DurableServerConsumption>(typeof(DurableServerConsumption).FullName!);
        if (Registration is null || consumption is null)
        {
            context.Reject(OpenIddictConstants.Errors.InvalidGrant);
            return;
        }
        revision = Registration.Revision;
        consumption.Claim(scope, context);
        if (!await consumption.IsCurrentAsync(scope, context, token) || !ClaimAssertion(token))
        {
            context.Reject(OpenIddictConstants.Errors.InvalidGrant);
            return;
        }
        var result = await ProtocolAdmissionProof.ReadAsync((PlatformDbContext)scope.Owner.Database, ownership, decision.SessionId, decision.MaximumAge,
            scope.Services.GetRequiredService<UserManager<PlatformUser>>(), scope.Services.GetRequiredService<NativeOperation>(), Clock, token);
        admission = result.Proof;
        claims = admission is null ? null : await admission.DescribeAsync(Clock, token);
        if (claims is null || context.Principal is null || !decision.IsCurrent(context) || (context.EndpointType == OpenIddictServerEndpointType.Token &&
            (!AdministratorDecisionPolicy.IsCode(decision.Original) || !AdministratorPrincipal.Matches(decision.Original, claims, revision))))
        {
            context.Reject(context.EndpointType == OpenIddictServerEndpointType.Token ? OpenIddictConstants.Errors.InvalidGrant :
                result.Outcome == ProtocolAdmissionOutcome.AccessDenied ? OpenIddictConstants.Errors.AccessDenied : OpenIddictConstants.Errors.LoginRequired);
            return;
        }
        if (scope.Services.GetService<BrowserProtocol>() is not null)
        {
            browser = new(scope, context, decision);
            if (!await browser.OpenAsync(policy, claims, token))
            {
                context.Reject(OpenIddictConstants.Errors.InvalidGrant);
                return;
            }
        }
        projected = AdministratorPrincipal.Project(context.Principal, claims, revision, decision.MaximumAge);
        browser?.Project(projected);
        publicClaims = PublicBinding(projected);
        context.Principal = projected;
    }

    private CeremonyClock Clock => scope.Services.GetRequiredService<CeremonyClock>();

    /// <summary>Token exchange requires its own already consumed client assertion; authorization relies on the consumed PAR reference.</summary>
    private bool ClaimAssertion(CancellationToken token)
    {
        if (context.EndpointType == OpenIddictServerEndpointType.Authorization)
        {
            return true;
        }
        assertion = context.Transaction.GetProperty<DurableClientAssertion>(typeof(DurableClientAssertion).FullName!);
        if (assertion is null)
        {
            return false;
        }
        assertion.Claim(scope, context);
        return assertion.IsCurrent(scope, context, Registration!, token);
    }

    /// <summary>Bounds actual prepared native principals without replacing them or extending an already shorter native expiration.</summary>
    internal void BoundExpiration()
    {
        if (claims is null || projected is null || !ReferenceEquals(projected, context.Principal))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        if (context.EndpointType == OpenIddictServerEndpointType.Authorization)
        {
            Bound(context.AuthorizationCodePrincipal, browser?.Bound(claims.CodeExpiresAt) ?? claims.CodeExpiresAt);
        }
        else
        {
            Bound(context.AccessTokenPrincipal, browser?.Bound(claims.TokenExpiresAt) ?? claims.TokenExpiresAt);
            Bound(context.IdentityTokenPrincipal, browser?.Bound(claims.TokenExpiresAt) ?? claims.TokenExpiresAt);
        }
    }

    /// <summary>Preserves all original proof fields and strict lifetime bounds after every final persisted-authority read.</summary>
    internal async ValueTask<bool> IsCurrentAsync(AdministratorRegistrationBinding registration, CancellationToken token)
    {
        if (decision is null || consumption is null || admission is null || claims is null || projected is null || Registration is null ||
            registration.ApplicationId != Registration.ApplicationId || registration.ScopeId != Registration.ScopeId ||
            registration.Revision != Registration.Revision || registration.ObservedAt < Registration.ObservedAt || !decision.IsCurrent(context) || !ReferenceEquals(projected, context.Principal) ||
            publicClaims != PublicBinding(projected) || !AdministratorDecisionPolicy.HasFixedBindings(projected) ||
            !await consumption.IsCurrentAsync(scope, context, token))
        {
            return false;
        }
        if (browser is not null)
        {
            await browser.RefreshAsync(token);
        }
        var current = await admission.DescribeAsync(Clock, token);
        if (current is null || !AdministratorPrincipal.Matches(projected, current, revision) ||
            (browser is not null && !browser.IsCurrent(current)) ||
            (context.EndpointType == OpenIddictServerEndpointType.Token &&
                (assertion is null || !assertion.IsCurrent(scope, context, registration, token) ||
                    projected.GetAuthorizationId() != decision.Original.GetAuthorizationId() || !AdministratorPrincipal.Matches(decision.Original, current, revision))))
        {
            return false;
        }
        return decision.IsCurrent(context) && ReferenceEquals(projected, context.Principal) &&
            AdministratorDecisionPolicy.HasFixedBindings(projected) && publicClaims == PublicBinding(projected) &&
            (context.EndpointType != OpenIddictServerEndpointType.Token || AdministratorDecisionPolicy.IsCode(decision.Original)) &&
            (context.EndpointType == OpenIddictServerEndpointType.Authorization
                ? AllowsExpiration(context.AuthorizationCodePrincipal, browser?.Bound(Math.Min(claims.CodeExpiresAt, current.CodeExpiresAt)) ?? Math.Min(claims.CodeExpiresAt, current.CodeExpiresAt))
                : AllowsExpiration(context.AccessTokenPrincipal, browser?.Bound(Math.Min(claims.TokenExpiresAt, current.TokenExpiresAt)) ?? Math.Min(claims.TokenExpiresAt, current.TokenExpiresAt)) &&
                    AllowsExpiration(context.IdentityTokenPrincipal, browser?.Bound(Math.Min(claims.TokenExpiresAt, current.TokenExpiresAt)) ?? Math.Min(claims.TokenExpiresAt, current.TokenExpiresAt)));
    }

    /// <summary>Clamps only a finite library-prepared expiration; missing preparation cannot be manufactured by this adapter.</summary>
    private static void Bound(ClaimsPrincipal? principal, long maximum)
    {
        if (principal?.GetExpirationDate() is not { } expiration)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        principal.SetExpirationDate(DateTimeOffset.FromUnixTimeSeconds(Math.Min(expiration.ToUnixTimeSeconds(), maximum)));
    }

    /// <summary>Rejects equality at actual final time and any widening of the original source-derived ceiling.</summary>
    private bool AllowsExpiration(ClaimsPrincipal? principal, long maximum) => principal?.GetExpirationDate() is { } expiration &&
        expiration > scope.Owner.Clock.GetUtcNow() && expiration.ToUnixTimeSeconds() <= maximum;

    /// <summary>Freezes all public projected claims and destinations while permitting later library-owned private authorization attachment.</summary>
    private static string PublicBinding(ClaimsPrincipal principal) => FactorBinding.Digest(principal.Claims
        .Where(claim => !claim.Type.StartsWith(OpenIddictConstants.Claims.Prefixes.Private, StringComparison.Ordinal))
        .Select(claim => new
        {
            claim.Type,
            claim.Value,
            claim.ValueType,
            claim.Issuer,
            claim.OriginalIssuer,
            Properties = claim.Properties.OrderBy(pair => pair.Key, StringComparer.Ordinal).ToArray()
        }).ToArray());
}
