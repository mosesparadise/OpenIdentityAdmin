using IdentityPlatform.AuthServer.Features.Protocol.Administrator;
using IdentityPlatform.AuthServer.Features.Protocol.Denials;
using IdentityPlatform.AuthServer.Features.Protocol.Hosting;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using OpenIddict.Server;

namespace IdentityPlatform.AuthServer.Features.Protocol.Ownership;

/// <summary>Forwards the installed native server dispatcher only within explicitly owned protocol storage.</summary>
internal sealed class OwnedServerDispatcher(OpenIddictServerDispatcher native, ProtocolStorageOwner owner, IServiceProvider services) : IOpenIddictServerDispatcher
{
    /// <summary>Preserves native event types/outcomes and nested cancellation; the enclosing request owns final cleanup.</summary>
    public ValueTask DispatchAsync<TContext>(TContext context) where TContext : OpenIddictServerEvents.BaseContext
    {
        ArgumentNullException.ThrowIfNull(context);
        if (services.GetService<ProtocolHostRequest>() is { } host && ProtocolStorageAmbient.Current is null)
        {
            return DispatchUnownedAsync(context, host);
        }
        return ProtocolStorageDispatch.RunAsync(owner, () => context.CancellationToken,
            token => context.CancellationToken = token, () => DispatchNativeAsync(context));
    }

    /// <summary>Preserves only the explicitly selected pilot transport or the outer unknown-path inference without activating owned stores.</summary>
    private async ValueTask DispatchUnownedAsync<TContext>(TContext context, ProtocolHostRequest host) where TContext : OpenIddictServerEvents.BaseContext
    {
        owner.RequireNeverActivated();
        host.RequireUnowned(context);
        await native.DispatchAsync(context);
        owner.RequireNeverActivated();
        if (context is OpenIddictServerEvents.ProcessRequestContext request)
        {
            host.RequireInferred(request);
        }
    }

    /// <summary>Routes only explicitly enabled fixed-client issuance into a fresh child; all other native paths preserve their dispatcher.</summary>
    private ValueTask DispatchNativeAsync<TContext>(TContext context) where TContext : OpenIddictServerEvents.BaseContext
    {
        if (context is OpenIddictServerEvents.ProcessRequestContext request && services.GetService<AdministratorAccountIssuanceEnabled>() is not null)
        {
            services.GetService<ProtocolHostRequest>()?.Track(request);
            return NativeErrorOrigin.TrackAsync(request, () => AdministratorPassthrough.TrackAsync(request, () => native.DispatchAsync(request)));
        }
        if (context is OpenIddictServerEvents.ProcessSignInContext signIn &&
            (signIn.EndpointType == OpenIddictServerEndpointType.PushedAuthorization ||
                (signIn.EndpointType is OpenIddictServerEndpointType.Authorization or OpenIddictServerEndpointType.Token &&
                    services.GetService<AdministratorAccountIssuanceEnabled>() is not null)) &&
            signIn.ClientId == AdministratorProtocolIdentity.ClientId && services.GetService<AdministratorIssuanceCoordinator>() is { } coordinator)
        {
            return coordinator.RunAsync(signIn);
        }
        if (context is OpenIddictServerEvents.ProcessErrorContext or OpenIddictServerEvents.ProcessChallengeContext &&
            NativeErrorOrigin.Find(context.Transaction) is not null && services.GetService<NativeErrorCoordinator>() is { } errors)
        {
            return errors.RunAsync((OpenIddictServerEvents.BaseRequestContext)(object)context);
        }
        if (context is OpenIddictServerEvents.ValidateAuthorizationRequestContext validation && NativeErrorOrigin.Find(context.Transaction) is { } origin)
        {
            return origin.ObserveValidationAsync(validation, () => native.DispatchAsync(validation));
        }
        return context is OpenIddictServerEvents.GenerateTokenContext generation && NativeIssuanceProof.Find(context.Transaction) is { } proof
            ? proof.GenerateAsync(generation, () => native.DispatchAsync(context)) : native.DispatchAsync(context);
    }
}
