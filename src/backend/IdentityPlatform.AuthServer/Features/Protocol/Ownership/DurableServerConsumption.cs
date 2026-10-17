using System.Security.Claims;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace IdentityPlatform.AuthServer.Features.Protocol.Ownership;

/// <summary>Binds one completed native redemption to its exact continuation and fresh issuance recheck.</summary>
internal sealed class DurableServerConsumption
{
    private readonly OpenIddictServerEvents.ProcessSignInContext context;
    private readonly ProtocolStorageRequest request;
    private readonly ClaimsPrincipal principal;
    private readonly ConsumptionStamp stamp;
    private readonly string binding;
    private ProtocolStorageOwner? issuanceOwner;
    private ProtocolStoragePhase? issuancePhase;
    private int claimed;

    /// <summary>Captures immutable original protected claims only after redemption commit and actual child cleanup.</summary>
    private DurableServerConsumption(OpenIddictServerEvents.ProcessSignInContext context, ClaimsPrincipal principal, ConsumptionStamp stamp,
        ProtocolStorageRequest request)
    {
        this.context = context;
        this.request = request;
        this.principal = principal;
        this.stamp = stamp;
        binding = Binding(principal);
    }

    /// <summary>Claims this receipt for exactly one fresh issuance epoch; later checks cannot transfer it to another owner or phase.</summary>
    internal void Claim(ProtocolStorageScope scope, OpenIddictServerEvents.ProcessSignInContext current)
    {
        if (Interlocked.Exchange(ref claimed, 1) != 0 || !ReferenceEquals(context, current) ||
            !ReferenceEquals(request, ProtocolStorageAmbient.Current?.Request) || !ReferenceEquals(scope, ProtocolStorageAmbient.Current?.Scope) ||
            scope.Owner.Transaction is null)
        {
            request.Fail();
            scope.Owner.Fail();
            throw ProtocolStorageOwner.Unavailable();
        }
        issuanceOwner = scope.Owner;
        issuancePhase = scope.Owner.Capture(write: true);
    }

    /// <summary>Publishes a typed continuation receipt only when native validation and native redemption both accept.</summary>
    internal static async ValueTask CaptureAsync(IServiceScopeFactory factory, OpenIddictServerEvents.ProcessSignInContext context,
        OpenIddictServerEvents.ValidateTokenContext validation)
    {
        context.Transaction.SetProperty<DurableServerConsumption>(typeof(DurableServerConsumption).FullName!, null);
        var original = ProtocolStorageAmbient.Current ?? throw ProtocolStorageOwner.Unavailable();
        var frozen = new ClaimsPrincipal(validation.Principal!.Identities.Select(identity => new ClaimsIdentity(identity)));
        var expected = Binding(frozen);
        await DurableServerToken.RunAsync(factory, validation,
            services => services.GetRequiredService<OpenIddictServerHandlers.RedeemTokenEntry>().HandleAsync(context),
            stamp =>
            {
                if (!ReferenceEquals(original.Request, ProtocolStorageAmbient.Current?.Request) ||
                    validation.Principal is null || expected != Binding(validation.Principal))
                {
                    validation.Reject(OpenIddictConstants.Errors.InvalidToken);
                }
                else if (!context.IsRejected && !context.IsRequestSkipped && !context.IsRequestHandled)
                {
                    context.Transaction.SetProperty(typeof(DurableServerConsumption).FullName!, new DurableServerConsumption(context, frozen, stamp, original.Request));
                }
            });
    }

    /// <summary>Checks the consumed row after reacquiring the profile lock without misclassifying this continuation as a replay.</summary>
    internal async ValueTask<bool> IsCurrentAsync(ProtocolStorageScope scope, OpenIddictServerEvents.ProcessSignInContext current, CancellationToken token)
    {
        if (!ReferenceEquals(issuanceOwner, scope.Owner) || issuancePhase is null || !ReferenceEquals(context, current) ||
            !ReferenceEquals(request, ProtocolStorageAmbient.Current?.Request) || !ReferenceEquals(scope, ProtocolStorageAmbient.Current?.Scope) ||
            scope.Owner.Transaction is null || !OriginalClaimsRemain() ||
            !string.Equals(current.ClientId, AdministratorProtocolIdentity.ClientId, StringComparison.Ordinal) ||
            !AdministratorNativeToken.IsCurrent(stamp.Expiration, scope.Owner.Clock))
        {
            return false;
        }
        scope.Owner.Check(issuancePhase, token);
        var profile = await scope.Owner.RunAsync(cancellation => new ValueTask<AdministratorProtocolProfile?>(scope.Owner.Database
            .Set<AdministratorProtocolProfile>().AsNoTracking().SingleOrDefaultAsync(row => row.Id == AdministratorProtocolProfile.SingletonId, cancellation)), token);
        if (profile is not { Enabled: true } || profile.Revision != stamp.Revision ||
            !string.Equals(profile.ApplicationId, stamp.ApplicationId, StringComparison.Ordinal))
        {
            return false;
        }
        var expiration = await AdministratorNativeToken.ExpirationAsync(scope, principal, principal.GetTokenType()!, profile.ApplicationId, token);
        var manager = scope.Services.GetRequiredService<IOpenIddictTokenManager>();
        var entry = await manager.FindByIdAsync(principal.GetTokenId()!, token);
        if (entry is null || !string.Equals(await manager.GetStatusAsync(entry, token), OpenIddictConstants.Statuses.Redeemed, StringComparison.Ordinal))
        {
            return false;
        }
        var authorized = await AuthorizationIsCurrentAsync(scope, token);
        scope.Owner.Check(issuancePhase, token);
        request.RequireCurrent();
        return authorized && OriginalClaimsRemain() && ReferenceEquals(scope, ProtocolStorageAmbient.Current?.Scope) &&
            AdministratorNativeToken.IsCurrent(expiration, scope.Owner.Clock) && AdministratorNativeToken.IsCurrent(stamp.Expiration, scope.Owner.Clock);
    }

    /// <summary>Rejects mutation of the original protected input or replacement of its code subject after durable consumption.</summary>
    private bool OriginalClaimsRemain()
    {
        var authentication = context.Transaction.GetProperty<OpenIddictServerEvents.ProcessAuthenticationContext>(
            typeof(OpenIddictServerEvents.ProcessAuthenticationContext).FullName!);
        var code = principal.HasTokenType(OpenIddictConstants.TokenTypeIdentifiers.Private.AuthorizationCode);
        var original = code ? authentication?.AuthorizationCodePrincipal : authentication?.RequestTokenPrincipal;
        return original is not null && binding == Binding(original) &&
            (code ? context.EndpointType == OpenIddictServerEndpointType.Token && context.Request.IsAuthorizationCodeGrantType() &&
                context.Principal is not null && string.Equals(context.Principal.GetClaim(OpenIddictConstants.Claims.Subject),
                    principal.GetClaim(OpenIddictConstants.Claims.Subject), StringComparison.Ordinal)
                : context.EndpointType == OpenIddictServerEndpointType.Authorization);
    }

    /// <summary>Freezes every protected claim and its destination metadata without retaining printable proof material.</summary>
    private static string Binding(ClaimsPrincipal value) => FactorBinding.Digest(value.Claims.Select(claim => new
    {
        claim.Type,
        claim.Value,
        claim.ValueType,
        claim.Issuer,
        claim.OriginalIssuer,
        Properties = claim.Properties.OrderBy(pair => pair.Key, StringComparer.Ordinal).ToArray()
    }).ToArray());

    /// <summary>Preserves native authorization status validation while intentionally avoiding the token replay-validation path.</summary>
    private async ValueTask<bool> AuthorizationIsCurrentAsync(ProtocolStorageScope scope, CancellationToken token)
    {
        if (string.IsNullOrEmpty(principal.GetAuthorizationId()))
        {
            return principal.HasTokenType(OpenIddictConstants.TokenTypeIdentifiers.Private.RequestToken);
        }
        var validation = new OpenIddictServerEvents.ValidateTokenContext(context.Transaction)
        {
            Principal = principal,
            TokenId = principal.GetTokenId(),
            AuthorizationId = principal.GetAuthorizationId()
        };
        var original = validation.CancellationToken;
        validation.CancellationToken = token;
        try
        {
            await scope.Services.GetRequiredService<OpenIddictServerHandlers.Protection.ValidateAuthorizationEntry>().HandleAsync(validation);
        }
        finally { validation.CancellationToken = original; }
        if (validation.IsRejected)
        {
            return false;
        }
        var manager = scope.Services.GetRequiredService<IOpenIddictAuthorizationManager>();
        var entry = await manager.FindByIdAsync(principal.GetAuthorizationId()!, token);
        return entry is not null &&
            string.Equals(await manager.GetApplicationIdAsync(entry, token), stamp.ApplicationId, StringComparison.Ordinal) &&
            string.Equals(await manager.GetSubjectAsync(entry, token), principal.GetClaim(OpenIddictConstants.Claims.Subject), StringComparison.Ordinal);
    }
}
