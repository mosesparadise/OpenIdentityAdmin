using IdentityPlatform.AuthServer.Features.Protocol.ClientAssertions;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace IdentityPlatform.AuthServer.Features.Protocol.Administrator;

/// <summary>Owns the fixed PAR issuance transaction from live registration through native persistence, final proof checks and commit.</summary>
internal sealed class AdministratorIssuance(ProtocolStorageScope scope, OpenIddictServerEvents.ProcessSignInContext context,
    AdministratorRegistrationPolicy policy) : IAsyncDisposable
{
    private ProtocolStorageTransaction? transaction;
    private AdministratorRegistrationBinding? registration;
    private DurableClientAssertion? assertion;
    private AdministratorTokenBudget? budget;
    private NativeIssuanceProof? native;
    private long maximumExpiration;

    /// <summary>Resolves only the state attached by the fresh response-child coordinator.</summary>
    internal static AdministratorIssuance? Find(OpenIddictServerTransaction transaction) =>
        transaction.GetProperty<AdministratorIssuance>(typeof(AdministratorIssuance).FullName!);

    /// <summary>Opens the issuance epoch after early durable work; non-PAR paths remain denied until their admission integration is installed.</summary>
    internal async ValueTask OpenAsync(CancellationToken token)
    {
        if (transaction is not null || context.EndpointType != OpenIddictServerEndpointType.PushedAuthorization)
        {
            context.Reject(OpenIddictConstants.Errors.InvalidRequest);
            return;
        }
        transaction = await ProtocolStorageTransaction.OpenAsync(scope.Owner);
        await AdministratorProtocolLocks.AcquireProfileAsync(scope.Owner, token);
        registration = await AdministratorRegistration.ReadLockedAsync(scope, policy, token);
        assertion = context.Transaction.GetProperty<DurableClientAssertion>(typeof(DurableClientAssertion).FullName!);
        if (registration is null || assertion is null)
        {
            context.Reject(OpenIddictConstants.Errors.InvalidClient);
            return;
        }
        assertion.Claim(scope, context);
        if (!assertion.IsCurrent(scope, context, registration, token))
        {
            context.Reject(OpenIddictConstants.Errors.InvalidClient);
        }
        maximumExpiration = checked(Math.Max(scope.Owner.Clock.GetUtcNow().ToUnixTimeSeconds(), registration.ObservedAt) +
            AdministratorAssertion.LifetimeSeconds);
    }

    /// <summary>Reserves the exact native row delta after installed generator evaluation and before native authorization/token creation.</summary>
    internal async ValueTask ReserveAsync(CancellationToken token)
    {
        var plan = AdministratorIssuancePlan.Read(context);
        if (registration is null || transaction is null || budget is not null || plan is null)
        {
            context.Reject(OpenIddictConstants.Errors.InvalidRequest);
            return;
        }
        budget = await AdministratorTokenBudget.ReserveLockedAsync(scope.Owner, registration.ApplicationId, plan.Creation,
            context.Options.TokenValidationParameters.ClockSkew, token);
        if (budget is not { HasCapacity: true })
        {
            context.Reject(OpenIddictConstants.Errors.TemporarilyUnavailable);
            return;
        }
        native = new(scope, context, plan, policy.RedirectUri);
        native.Attach();
    }

    /// <summary>Bounds the prepared native request principal before any token creation or cryptographic signing occurs.</summary>
    internal void BoundExpiration()
    {
        if (native is null || context.RequestTokenPrincipal is not { } principal ||
            principal.GetClaim(OpenIddictConstants.Claims.Private.RequestTokenType) != OpenIddictConstants.RequestTokenTypes.Private.PushedAuthorizationRequest ||
            principal.GetExpirationDate() is not { } expiration)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        principal.SetExpirationDate(DateTimeOffset.FromUnixTimeSeconds(Math.Min(expiration.ToUnixTimeSeconds(), maximumExpiration)));
    }

    /// <summary>Requires actual native creation/formatting, exact final counts and current proof immediately before the one issuance commit.</summary>
    internal async ValueTask<bool> CommitAsync(CancellationToken token)
    {
        if (transaction is null || registration is null || assertion is null || native is null || budget is null ||
            !await native.VerifyAsync(registration.ApplicationId, token) || !await budget.IsCurrentAsync(token))
        {
            return false;
        }
        var current = await AdministratorRegistration.ReadLockedAsync(scope, policy, token);
        if (current is null || !assertion.IsCurrent(scope, context, current, token) ||
            context.RequestTokenPrincipal?.GetExpirationDate() is not { } expiration ||
            expiration.ToUnixTimeSeconds() > maximumExpiration || expiration <= scope.Owner.Clock.GetUtcNow())
        {
            return false;
        }
        await transaction.CommitAsync(token);
        return true;
    }

    /// <summary>Allows safe quota cleanup progress only for the explicit capacity-denied path with zero native creation.</summary>
    internal async ValueTask CompleteDenialAsync(CancellationToken token)
    {
        if (transaction is not null && budget is { HasCapacity: false } && await budget.CanCommitCleanupAsync(token))
        {
            await transaction.CommitAsync(token);
        }
    }

    /// <summary>Retains the real transaction cleanup; successful return never substitutes for its eventual actual completion.</summary>
    public ValueTask DisposeAsync() => transaction?.DisposeAsync() ?? ValueTask.CompletedTask;
}
