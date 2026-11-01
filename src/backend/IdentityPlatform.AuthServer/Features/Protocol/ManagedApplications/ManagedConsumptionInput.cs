using System.Security.Claims;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace IdentityPlatform.AuthServer.Features.Protocol.ManagedApplications;

/// <summary>Freezes the original authenticated redemption input before any native child can await or mutate its shared transaction.</summary>
internal sealed class ManagedConsumptionInput
{
    private readonly OpenIddictServerEvents.ProcessSignInContext context;
    private readonly OpenIddictServerEvents.ProcessAuthenticationContext authentication;
    private readonly OpenIddictServerEndpointType endpoint;
    private readonly OpenIddictRequest parameters;
    private readonly string parameterDigest;
    private readonly string principalDigest;
    private readonly ProtocolStorageRequest request;
    internal ClaimsPrincipal Principal
    {
        get;
    }

    /// <summary>Captures identity and exact parameter/claim values before the durable validation/redemption child opens.</summary>
    internal ManagedConsumptionInput(OpenIddictServerEvents.ProcessSignInContext context)
    {
        this.context = context;
        authentication = Authentication(context) ?? throw ProtocolStorageOwner.Unavailable();
        endpoint = context.EndpointType;
        parameters = context.Request;
        parameterDigest = ManagedConsumption.Parameters(parameters);
        Principal = ManagedConsumption.Original(context) ?? throw ProtocolStorageOwner.Unavailable();
        principalDigest = ManagedConsumption.Claims(Principal);
        request = ProtocolStorageAmbient.Current!.Request;
    }

    /// <summary>Rejects substitutions before receipt creation and throughout the later issuance owner.</summary>
    internal bool Current() => ReferenceEquals(request, ProtocolStorageAmbient.Current?.Request) && context.EndpointType == endpoint &&
        !authentication.IsRejected && !authentication.IsRequestSkipped && !authentication.IsRequestHandled &&
        ReferenceEquals(authentication, Authentication(context)) && ReferenceEquals(parameters, context.Request) &&
        parameterDigest == ManagedConsumption.Parameters(context.Request) && ReferenceEquals(Principal, ManagedConsumption.Original(context)) &&
        principalDigest == ManagedConsumption.Claims(Principal) && !context.IsRejected && !context.IsRequestSkipped;

    /// <summary>Reads only the native authentication context already attached by actual token validation.</summary>
    private static OpenIddictServerEvents.ProcessAuthenticationContext? Authentication(OpenIddictServerEvents.ProcessSignInContext context) =>
        context.Transaction.GetProperty<OpenIddictServerEvents.ProcessAuthenticationContext>(typeof(OpenIddictServerEvents.ProcessAuthenticationContext).FullName!);
}
