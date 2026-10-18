using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace IdentityPlatform.AuthServer.Features.Protocol.Administrator;

/// <summary>Observes actual native ad-hoc authorization creation and binds every issued credential to the exact persisted row.</summary>
internal sealed class NativeIssuedAuthorization(ProtocolStorageScope scope, OpenIddictServerEvents.ProcessSignInContext context)
{
    private bool observed;
    private string? identifier;
    private string? descriptorBinding;

    /// <summary>Forwards the actual native attachment once, requiring a new authorization only when this request issues a code.</summary>
    internal async ValueTask ObserveAsync(Func<ValueTask> native)
    {
        if (observed || context.Principal is null)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        observed = true;
        var before = context.Principal.GetAuthorizationId();
        if (context.EndpointType == OpenIddictServerEndpointType.Authorization && !string.IsNullOrEmpty(before))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        await native();
        identifier = context.Principal.GetAuthorizationId();
        if (context.EndpointType == OpenIddictServerEndpointType.PushedAuthorization)
        {
            if (!string.IsNullOrEmpty(identifier))
            {
                throw ProtocolStorageOwner.Unavailable();
            }
            return;
        }
        if (string.IsNullOrEmpty(identifier) || (context.EndpointType == OpenIddictServerEndpointType.Token && identifier != before))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        var descriptor = await ReadAsync(context.CancellationToken);
        if (descriptor is null)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        descriptorBinding = Binding(descriptor);
    }

    /// <summary>Rechecks attachment after awaited token and authorization reads without accepting later identifier substitution.</summary>
    internal bool IsBound() => observed && context.Principal is not null && identifier == context.Principal.GetAuthorizationId();

    /// <summary>Requires the exact row and descriptor after all native generation/formatting, independently of aggregate quota counts.</summary>
    internal async ValueTask<bool> VerifyAsync(string application, CancellationToken token)
    {
        if (!IsBound() || context.Principal is null)
        {
            return false;
        }
        if (context.EndpointType == OpenIddictServerEndpointType.PushedAuthorization)
        {
            return string.IsNullOrEmpty(identifier);
        }
        var descriptor = await ReadAsync(token);
        return descriptor is not null && descriptorBinding == Binding(descriptor) && descriptor.ApplicationId == application &&
            descriptor.Status == OpenIddictConstants.Statuses.Valid && descriptor.Type == OpenIddictConstants.AuthorizationTypes.AdHoc &&
            descriptor.Subject == context.Principal.GetClaim(OpenIddictConstants.Claims.Subject) && descriptor.CreationDate is not null &&
            descriptor.Scopes.SetEquals(context.Principal.GetScopes());
    }

    /// <summary>Reloads the exact native entity in the owning epoch before inspecting its persistent descriptor.</summary>
    private async ValueTask<OpenIddictAuthorizationDescriptor?> ReadAsync(CancellationToken token)
    {
        var manager = scope.Services.GetRequiredService<IOpenIddictAuthorizationManager>();
        var row = identifier is null ? null : await manager.FindByIdAsync(identifier, token);
        if (row is null || !await ProtocolStorageRows.ReloadAsync(scope, row, token))
        {
            return null;
        }
        var descriptor = new OpenIddictAuthorizationDescriptor();
        await manager.PopulateAsync(descriptor, row, token);
        return descriptor;
    }

    /// <summary>Freezes persistent authorization metadata without emitting principal or credential material.</summary>
    private static string Binding(OpenIddictAuthorizationDescriptor descriptor) => FactorBinding.Digest(new
    {
        descriptor.ApplicationId,
        descriptor.CreationDate,
        descriptor.Status,
        descriptor.Subject,
        descriptor.Type,
        Scopes = descriptor.Scopes.Order(StringComparer.Ordinal).ToArray(),
        Properties = descriptor.Properties.OrderBy(pair => pair.Key, StringComparer.Ordinal).ToArray()
    });
}

/// <summary>Preserves the installed authorization attachment and original native descriptor filters/order.</summary>
internal sealed class NativeAuthorizationCreation(OpenIddictServerHandlers.AttachAuthorization native)
    : IOpenIddictServerHandler<OpenIddictServerEvents.ProcessSignInContext>
{
    /// <summary>Observes the concrete native handler only when an owned issuance proof is attached.</summary>
    public ValueTask HandleAsync(OpenIddictServerEvents.ProcessSignInContext context) =>
        NativeIssuanceProof.Find(context.Transaction) is { } proof
            ? proof.AttachAuthorizationAsync(() => native.HandleAsync(context)) : native.HandleAsync(context);
}
