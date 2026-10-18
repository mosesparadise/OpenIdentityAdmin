using System.Security.Claims;
using IdentityPlatform.Admin.Bff.Features.Authentication.Protocol;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using OpenIddict.Abstractions;
using OpenIddict.Client;

namespace IdentityPlatform.Admin.Bff.Features.Authentication.Ownership;

/// <summary>Commits actual native reference-state creation in an isolated child before the challenge may send PAR.</summary>
internal sealed class DurableClientState(IServiceScopeFactory factory, NativeClientProfile? profile = null) : IOpenIddictClientHandler<OpenIddictClientEvents.ProcessChallengeContext>
{
    /// <summary>Retains the original native principal and proves its stored reference before publishing the state to later native handlers.</summary>
    public async ValueTask HandleAsync(OpenIddictClientEvents.ProcessChallengeContext context)
    {
        var principal = context.StateTokenPrincipal;
        var parent = ProtocolStorageAmbient.Current ?? throw ProtocolStorageOwner.Unavailable();
        if (context.Registration.RegistrationId != AdministratorProtocolIdentity.ClientId || context.Options.DisableTokenStorage ||
            principal is null || !IsCurrent(principal, parent.Request.Clock))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        if (profile is not null)
        {
            NativeClientChallenge.RequireState(context, profile);
        }
        var binding = new NativeStateBinding(context);
        var proof = await ProtocolStorageChild.RunAsync(factory, (scope, cancellation) => CreateAsync(scope, context, binding, cancellation));
        binding.RequireCurrent();
        if (proof is null || !IsCurrent(principal, parent.Request.Clock))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        proof.Release();
        if (profile is not null)
        {
            NativeClientContinuation.Attach(context, profile);
        }
    }

    /// <summary>Completes native generation and authoritative row reconciliation inside the single quota-owning transaction.</summary>
    private static async ValueTask<NativeStateGeneration?> CreateAsync(ProtocolStorageScope scope,
        OpenIddictClientEvents.ProcessChallengeContext context, NativeStateBinding binding, CancellationToken cancellation)
    {
        await using var transaction = await ProtocolStorageTransaction.OpenAsync(scope.Owner);
        await AdministratorProtocolLocks.AcquireClientStateAsync(scope.Owner, cancellation);
        var capacity = await AdministratorStateCapacity.ReserveLockedAsync(scope.Owner, context.Options.TokenValidationParameters.ClockSkew, cancellation);
        if (capacity is null)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        if (!capacity.HasCapacity)
        {
            await CommitCleanupAsync(capacity, transaction, cancellation);
            return null;
        }
        var proof = new NativeStateGeneration(scope, context, binding);
        await scope.Services.GetRequiredService<OpenIddictClientHandlers.GenerateLoginStateToken>().HandleAsync(context);
        proof.RequireCompleted();
        if (!await capacity.CanCommitAsync(created: true, cancellation) || !await MatchesStoredAsync(scope, context, proof, cancellation) ||
            !IsCurrent(context.StateTokenPrincipal!, scope.Owner.Clock))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        proof.RequireCompleted();
        await transaction.CommitAsync(cancellation);
        return proof;
    }

    /// <summary>Allows bounded cleanup to make progress at capacity without creating a state or claiming challenge success.</summary>
    private static async Task CommitCleanupAsync(AdministratorStateCapacity capacity, ProtocolStorageTransaction transaction, CancellationToken token)
    {
        if (!await capacity.CanCommitAsync(created: false, token))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        await transaction.CommitAsync(token);
    }

    /// <summary>Requires finite fixed-registration state dates; caller overrides cannot widen the five-minute lifetime.</summary>
    internal static bool IsCurrent(ClaimsPrincipal principal, TimeProvider clock)
    {
        var created = principal.GetCreationDate();
        var expires = principal.GetExpirationDate();
        var now = clock.GetUtcNow();
        return principal.GetClaim(OpenIddictConstants.Claims.Private.RegistrationId) == AdministratorProtocolIdentity.ClientId &&
            created is { } issued && expires is { } end && issued <= now + TimeSpan.FromSeconds(AdministratorAssertion.ClockSkewSeconds) &&
            end > now && end > issued && end - issued <= TimeSpan.FromSeconds(AdministratorStateCapacity.LifetimeSeconds);
    }

    /// <summary>Resolves the native reference through its real manager and verifies its exact row, status and bounded dates.</summary>
    private static async ValueTask<bool> MatchesStoredAsync(ProtocolStorageScope scope, OpenIddictClientEvents.ProcessChallengeContext context,
        NativeStateGeneration proof, CancellationToken token)
    {
        if (string.IsNullOrEmpty(context.StateToken) || string.IsNullOrEmpty(context.StateTokenPrincipal?.GetTokenId()))
        {
            return false;
        }
        return await NativeStatePersistence.MatchesAsync(scope, context, proof, token);
    }
}
