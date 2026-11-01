using System.Security.Claims;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace IdentityPlatform.AuthServer.Features.Protocol.ManagedApplications;

/// <summary>Binds one completed native redemption to its original sign-in and one later issuance owner without exporting scoped services.</summary>
internal sealed class ManagedConsumption
{
    private readonly OpenIddictServerEvents.ProcessSignInContext context;
    private readonly ManagedConsumptionInput input;
    private readonly string rowDigest;
    private ProtocolStorageScope? issuance;
    private ProtocolStoragePhase? phase;

    /// <summary>Captures immutable source only after the native redemption child's commit and actual cleanup succeed.</summary>
    private ManagedConsumption(OpenIddictServerEvents.ProcessSignInContext context, ManagedConsumptionInput input, ManagedTokenBinding binding)
    {
        this.context = context;
        this.input = input;
        rowDigest = FactorBinding.Digest(binding.Row);
    }

    /// <summary>Finds only the private receipt attached after completed native redemption.</summary>
    internal static ManagedConsumption? Find(OpenIddictServerTransaction transaction) => transaction.GetProperty<ManagedConsumption>(typeof(ManagedConsumption).FullName!);

    /// <summary>Runs the actual native redemption slot after full request authentication and PKCE validation have completed.</summary>
    internal static async ValueTask RedeemAsync(IServiceScopeFactory factory, OpenIddictServerEvents.ProcessSignInContext context)
    {
        var input = new ManagedConsumptionInput(context);
        var original = input.Principal;
        var validation = new OpenIddictServerEvents.ValidateTokenContext(context.Transaction)
        {
            Principal = original,
            TokenId = original.GetTokenId(),
            AuthorizationId = original.GetAuthorizationId()
        };
        var result = await ManagedDurableToken.RunAsync(factory, validation, context);
        if (validation.IsRejected)
        {
            context.Reject(validation.Error, validation.ErrorDescription, validation.ErrorUri);
            return;
        }
        if (result is null || Find(context.Transaction) is not null || context.IsRejected || context.IsRequestSkipped || context.IsRequestHandled ||
            !input.Current())
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        context.Transaction.SetProperty(typeof(ManagedConsumption).FullName!, new ManagedConsumption(context, input, result));
    }

    /// <summary>Claims the original receipt once for exactly one fresh write phase.</summary>
    internal void Claim(ProtocolStorageScope scope, OpenIddictServerEvents.ProcessSignInContext current)
    {
        if (issuance is not null || !ReferenceEquals(current, context) || !input.Current())
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        issuance = scope;
        phase = scope.Owner.Capture(write: true);
    }

    /// <summary>Checks original claims, native redeemed status, unchanged managed row and current registration after reacquiring serialization.</summary>
    internal async ValueTask<bool> VerifyAsync(ProtocolStorageScope scope, CancellationToken token, bool originalRow = true)
    {
        if (!ReferenceEquals(issuance, scope) || phase is null || !input.Current())
        {
            return false;
        }
        scope.Owner.Check(phase, token);
        var original = input.Principal;
        var binding = await ManagedTokenBinding.ReadAsync(scope, original, context.ClientId!, token);
        var manager = scope.Services.GetRequiredService<IOpenIddictTokenManager>();
        var entry = await manager.FindByIdAsync(original.GetTokenId()!, token);
        if (binding is null || (originalRow && rowDigest != FactorBinding.Digest(binding.Row)) || entry is null ||
            await manager.GetStatusAsync(entry, token) != OpenIddictConstants.Statuses.Redeemed)
        {
            return false;
        }
        if (original.GetAuthorizationId() is { } identifier)
        {
            var authorizations = scope.Services.GetRequiredService<IOpenIddictAuthorizationManager>();
            var authorization = await authorizations.FindByIdAsync(identifier, token);
            if (authorization is null || !await ProtocolStorageRows.ReloadAsync(scope, authorization, token) ||
                await authorizations.GetStatusAsync(authorization, token) != OpenIddictConstants.Statuses.Valid)
            {
                return false;
            }
        }
        return input.Current() && binding.Expiration > scope.Owner.Clock.GetUtcNow();
    }

    /// <summary>Reads the actual protected artifact from native authentication without using the newly projected issuance principal.</summary>
    internal static ClaimsPrincipal? Original(OpenIddictServerEvents.ProcessSignInContext context)
    {
        var authentication = context.Transaction.GetProperty<OpenIddictServerEvents.ProcessAuthenticationContext>(typeof(OpenIddictServerEvents.ProcessAuthenticationContext).FullName!);
        return context.EndpointType == OpenIddictServerEndpointType.Authorization ? authentication?.RequestTokenPrincipal : authentication?.AuthorizationCodePrincipal;
    }

    /// <summary>Hashes every protected claim and destination solely for in-request mutation detection.</summary>
    internal static string Claims(ClaimsPrincipal principal) => ProtocolTokenDescriptorSnapshot.Claims(principal);

    /// <summary>Hashes exact native parameters without logging or retaining printable credential material.</summary>
    internal static string Parameters(OpenIddictRequest parameters) => FactorBinding.Digest(parameters.GetParameters().OrderBy(pair => pair.Key, StringComparer.Ordinal)
        .Select(pair => new { pair.Key, Value = pair.Value.ToString() }).ToArray());
}
