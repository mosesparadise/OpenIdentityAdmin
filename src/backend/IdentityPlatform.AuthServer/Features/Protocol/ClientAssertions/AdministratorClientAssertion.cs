using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace IdentityPlatform.AuthServer.Features.Protocol.ClientAssertions;

/// <summary>Consumes the fixed client's native-validated assertion independently before any PAR or authorization-code grant processing.</summary>
internal sealed class AdministratorClientAssertion(IServiceScopeFactory factory, AdministratorRegistrationPolicy policy)
    : IOpenIddictServerHandler<OpenIddictServerEvents.ProcessAuthenticationContext>
{
    /// <summary>Commits exactly once, retains cleanup, and accepts only a still-current proof after the durable boundary.</summary>
    public async ValueTask HandleAsync(OpenIddictServerEvents.ProcessAuthenticationContext context)
    {
        if (!Applies(context))
        {
            return;
        }
        var proof = context.Transaction.GetProperty<ClientAssertionProof>(typeof(ClientAssertionProof).FullName!);
        if (proof is null || !proof.Claim(context))
        {
            context.Reject(OpenIddictConstants.Errors.InvalidClient);
            return;
        }
        var key = typeof(DurableClientAssertion).FullName!;
        if (context.Transaction.GetProperty<DurableClientAssertion>(key) is not null)
        {
            context.Reject(OpenIddictConstants.Errors.InvalidClient);
            return;
        }
        var receipt = await DurableClientAssertion.ConsumeAsync(factory, policy, context, proof);
        if (receipt is null)
        {
            context.Reject(OpenIddictConstants.Errors.InvalidClient);
        }
        else
        {
            context.Transaction.SetProperty(key, receipt);
        }
    }

    /// <summary>Limits the proof-consuming boundary to intentional fixed-client PAR and token endpoint authentication.</summary>
    internal static bool Applies(OpenIddictServerEvents.ProcessAuthenticationContext context) =>
        context.ClientId == AdministratorProtocolIdentity.ClientId &&
        context.EndpointType is OpenIddictServerEndpointType.PushedAuthorization or OpenIddictServerEndpointType.Token;
}

/// <summary>Bounds the fixed proof before native JWT parsing and prevents alternative client authentication methods.</summary>
internal sealed class AdministratorAssertionGuard : IOpenIddictServerHandler<OpenIddictServerEvents.ProcessAuthenticationContext>
{
    /// <summary>Requires the exact form identity/method and an ASCII compact assertion within the approved bound.</summary>
    public ValueTask HandleAsync(OpenIddictServerEvents.ProcessAuthenticationContext context)
    {
        if (AdministratorClientAssertion.Applies(context) && !HasBoundedProof(context))
        {
            context.Reject(OpenIddictConstants.Errors.InvalidClient);
        }
        return ValueTask.CompletedTask;
    }

    /// <summary>Shares only the established fixed-client input shape, without expanding any caller's endpoint eligibility.</summary>
    internal static bool HasBoundedProof(OpenIddictServerEvents.ProcessAuthenticationContext context) =>
        context.Request.ClientId == AdministratorProtocolIdentity.ClientId &&
        context.ClientAssertion is { Length: > 0 and <= ClientAssertionShape.MaximumBytes } assertion && assertion.All(char.IsAscii) &&
        context.ClientAssertionType == OpenIddictConstants.ClientAssertionTypes.JwtBearer && string.IsNullOrEmpty(context.Request.ClientSecret);
}

/// <summary>Captures only completed native validation before its caller promotes the assertion principal to authenticated-client evidence.</summary>
internal sealed class AdministratorAssertionCapture(AdministratorRegistrationPolicy policy)
    : IOpenIddictServerHandler<OpenIddictServerEvents.ValidateTokenContext>
{
    /// <summary>Retains one original proof; missing native crypto or duplicate capture fails closed.</summary>
    public ValueTask HandleAsync(OpenIddictServerEvents.ValidateTokenContext context)
    {
        if (context.Request?.ClientId == AdministratorProtocolIdentity.ClientId &&
            context.ValidTokenTypes.SetEquals([OpenIddictConstants.TokenTypeIdentifiers.Private.ClientAssertion]))
        {
            var key = typeof(ClientAssertionProof).FullName!;
            var proof = ClientAssertionProof.Capture(context, policy);
            if (proof is null || context.Transaction.GetProperty<ClientAssertionProof>(key) is not null)
            {
                context.Reject(OpenIddictConstants.Errors.InvalidClient);
            }
            else
            {
                context.Transaction.SetProperty(key, proof);
            }
        }
        return ValueTask.CompletedTask;
    }
}
