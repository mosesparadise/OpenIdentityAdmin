using IdentityPlatform.AuthServer.Features.Protocol.Administrator;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using IdentityPlatform.Infrastructure.Features.ProtocolTransport;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using OpenIddict.Server;
using OpenIddict.Server.AspNetCore;

namespace IdentityPlatform.Host.Tests.Features.ProtocolStorage;

/// <summary>Exercises the installed native passthrough handler with real owned response staging, without issuing any credential.</summary>
internal sealed class AdministratorPassthroughProbe(bool authorization) : IAsyncDisposable
{
    private readonly StorageRequestProbe storage = new();
    internal HttpContext Http { get; } = new DefaultHttpContext();
    internal OpenIddictServerEvents.ProcessRequestContext Outer { get; private set; } = null!;
    internal OpenIddictServerEvents.BaseRequestContext Inner { get; private set; } = null!;
    internal OpenIddictServerEvents.ProcessSignInContext SignIn { get; private set; } = null!;
    internal Microsoft.Extensions.DependencyInjection.IServiceScopeFactory Factory => storage.Factory;

    /// <summary>Creates the actual request lifetime and capture while leaving every relational connection unopened.</summary>
    internal Task RunAsync(Func<AdministratorPassthroughProbe, Task> operation)
    {
        var request = new ProtocolStorageRequest(storage.Clock, TestContext.Current.CancellationToken);
        return request.RunAsync(storage.Factory, async (scope, _) =>
        {
            using var capture = new ProtocolResponseCapture(request);
            Http.RequestServices = scope.Services;
            Http.Features.Set<IHttpResponseFeature>(capture);
            Http.Features.Set<IHttpResponseBodyFeature>(capture);
            var transaction = new OpenIddictServerTransaction
            {
                Options = new(),
                Request = new() { ClientId = AdministratorProtocolIdentity.ClientId },
                EndpointType = authorization ? OpenIddictServerEndpointType.Authorization : OpenIddictServerEndpointType.Token
            };
            transaction.Properties[typeof(HttpRequest).FullName!] = new WeakReference<HttpRequest>(Http.Request);
            Outer = new(transaction);
            Inner = authorization ? new OpenIddictServerEvents.HandleAuthorizationRequestContext(transaction) :
                new OpenIddictServerEvents.HandleTokenRequestContext(transaction);
            SignIn = new(transaction);
            await operation(this);
            return true;
        });
    }

    /// <summary>Propagates the observed native inner skip to its outer context for these focused receipt-state controls.</summary>
    internal ValueTask HandoffAsync(bool native = true) => AdministratorPassthrough.TrackAsync(Outer, async () =>
    {
        if (native)
        {
            await NativeAsync();
        }
        else
        {
            Inner.SkipRequest();
        }
        Outer.SkipRequest();
    });

    /// <summary>Calls the actual installed passthrough implementation through its production observation wrapper.</summary>
    internal ValueTask NativeAsync() => authorization
        ? new NativeAdministratorPassthrough<OpenIddictServerEvents.HandleAuthorizationRequestContext,
            OpenIddictServerAspNetCoreHandlerFilters.RequireAuthorizationEndpointPassthroughEnabled>(new())
            .HandleAsync((OpenIddictServerEvents.HandleAuthorizationRequestContext)Inner)
        : new NativeAdministratorPassthrough<OpenIddictServerEvents.HandleTokenRequestContext,
            OpenIddictServerAspNetCoreHandlerFilters.RequireTokenEndpointPassthroughEnabled>(new())
            .HandleAsync((OpenIddictServerEvents.HandleTokenRequestContext)Inner);

    /// <summary>Disposes all fixture services after the enclosing request completes or rejects.</summary>
    public ValueTask DisposeAsync() => storage.DisposeAsync();
}
