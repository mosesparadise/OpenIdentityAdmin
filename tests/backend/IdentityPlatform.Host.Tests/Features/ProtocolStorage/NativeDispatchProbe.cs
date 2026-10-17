using System.Text.Encodings.Web;
using IdentityPlatform.Admin.Bff.Features.Authentication.Ownership;
using IdentityPlatform.AuthServer.Features.Protocol.Ownership;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Client;
using OpenIddict.Client.AspNetCore;
using OpenIddict.Server;
using OpenIddict.Server.AspNetCore;

namespace IdentityPlatform.Host.Tests.Features.ProtocolStorage;

/// <summary>Runs the installed ASP.NET handler and dispatcher using synthetic event handlers from the owned child provider.</summary>
internal sealed class NativeDispatchProbe : IAsyncDisposable
{
    private readonly bool client;
    internal StorageRequestProbe Storage
    {
        get;
    }
    internal Func<NativeEvent, ValueTask> Request { get; set; } = _ => ValueTask.CompletedTask;
    internal Func<NativeEvent, ValueTask> Error { get; set; } = _ => ValueTask.CompletedTask;
    internal Func<bool> Filter { get; set; } = () => true;
    internal Func<NativeEvent, ValueTask> Nested { get; set; } = _ => ValueTask.CompletedTask;
    internal ProtocolStorageScope Active { get; private set; } = null!;
    internal HttpContext Http { get; private set; } = null!;
    internal CancellationToken? IncomingToken
    {
        get; set;
    }
    internal CancellationToken InitialToken
    {
        get; private set;
    }

    /// <summary>Registers actual native dispatchers, factories and ASP.NET handlers without production profile configuration.</summary>
    internal NativeDispatchProbe(bool client)
    {
        this.client = client;
        Storage = new StorageRequestProbe(Configure);
    }

    /// <summary>Resolves the entire native graph from the explicitly owned provider and retains it through error dispatch.</summary>
    internal Task<bool> RunAsync(ProtocolStorageRequest request) => request.RunAsync(Storage.Factory, InvokeAsync);

    /// <summary>Exercises the actual native ASP.NET request-to-error transition.</summary>
    private async ValueTask<bool> InvokeAsync(ProtocolStorageScope scope, CancellationToken token)
    {
        Active = scope;
        InitialToken = IncomingToken ?? token;
        Http = new DefaultHttpContext { RequestServices = scope.Services, RequestAborted = InitialToken };
        IAuthenticationRequestHandler handler;
        string name;
        if (client)
        {
            handler = ActivatorUtilities.CreateInstance<OpenIddictClientAspNetCoreHandler>(scope.Services);
            name = OpenIddictClientAspNetCoreDefaults.AuthenticationScheme;
        }
        else
        {
            handler = ActivatorUtilities.CreateInstance<OpenIddictServerAspNetCoreHandler>(scope.Services);
            name = OpenIddictServerAspNetCoreDefaults.AuthenticationScheme;
        }
        await handler.InitializeAsync(new AuthenticationScheme(name, name, handler.GetType()), Http);
        return await handler.HandleRequestAsync();
    }

    /// <summary>Dispatches a real nested native authentication event using the same native transaction.</summary>
    internal ValueTask DispatchNestedAsync() => client ?
        Active.Services.GetRequiredService<IOpenIddictClientDispatcher>().DispatchAsync(
            new OpenIddictClientEvents.ProcessAuthenticationContext(Http.Features.Get<OpenIddictClientAspNetCoreFeature>()!.Transaction!)) :
        Active.Services.GetRequiredService<IOpenIddictServerDispatcher>().DispatchAsync(
            new OpenIddictServerEvents.ProcessAuthenticationContext(Http.Features.Get<OpenIddictServerAspNetCoreFeature>()!.Transaction!));

    internal CancellationToken CurrentToken => client ? Http.Features.Get<OpenIddictClientAspNetCoreFeature>()!.Transaction!.CancellationToken :
        Http.Features.Get<OpenIddictServerAspNetCoreFeature>()!.Transaction!.CancellationToken;

    /// <summary>Installs only synthetic handlers while keeping the native dispatch loop and ASP.NET control flow real.</summary>
    private void Configure(IServiceCollection services)
    {
        services.AddSingleton(UrlEncoder.Default);
        if (client)
        {
            services.AddScoped<ClientFilter>(_ => new(() => Filter()));
            services.AddOptions<OpenIddictClientAspNetCoreOptions>();
            services.AddOptions<OpenIddictClientOptions>().Configure(options =>
            {
                options.Handlers.Clear();
                options.Handlers.Add(OpenIddictClientHandlerDescriptor.CreateBuilder<OpenIddictClientEvents.ProcessRequestContext>()
                    .UseInlineHandler(context => Request(Wrap(context))).AddFilter<ClientFilter>().Build());
                options.Handlers.Add(OpenIddictClientHandlerDescriptor.CreateBuilder<OpenIddictClientEvents.ProcessErrorContext>()
                    .UseInlineHandler(context => Error(Wrap(context))).Build());
                options.Handlers.Add(OpenIddictClientHandlerDescriptor.CreateBuilder<OpenIddictClientEvents.ProcessAuthenticationContext>()
                    .UseInlineHandler(context => Nested(Wrap(context))).Build());
            });
            services.AddScoped<IOpenIddictClientFactory, OpenIddictClientFactory>();
            services.AddScoped<IOpenIddictClientDispatcher, OpenIddictClientDispatcher>();
            services.AddOwnedClientDispatcher();
        }
        else
        {
            services.AddScoped<ServerFilter>(_ => new(() => Filter()));
            services.AddOptions<OpenIddictServerAspNetCoreOptions>();
            services.AddOptions<OpenIddictServerOptions>().Configure(options =>
            {
                options.Handlers.Clear();
                options.Handlers.Add(OpenIddictServerHandlerDescriptor.CreateBuilder<OpenIddictServerEvents.ProcessRequestContext>()
                    .UseInlineHandler(context => Request(Wrap(context))).AddFilter<ServerFilter>().Build());
                options.Handlers.Add(OpenIddictServerHandlerDescriptor.CreateBuilder<OpenIddictServerEvents.ProcessErrorContext>()
                    .UseInlineHandler(context => Error(Wrap(context))).Build());
                options.Handlers.Add(OpenIddictServerHandlerDescriptor.CreateBuilder<OpenIddictServerEvents.ProcessAuthenticationContext>()
                    .UseInlineHandler(context => Nested(Wrap(context))).Build());
            });
            services.AddScoped<IOpenIddictServerFactory, OpenIddictServerFactory>();
            services.AddScoped<IOpenIddictServerDispatcher, OpenIddictServerDispatcher>();
            services.AddOwnedServerDispatcher();
        }
    }

    /// <summary>Invokes the public native dispatcher contract with its invalid null context input.</summary>
    internal ValueTask DispatchNullAsync() => client ?
        Active.Services.GetRequiredService<IOpenIddictClientDispatcher>().DispatchAsync<OpenIddictClientEvents.ProcessRequestContext>(null!) :
        Active.Services.GetRequiredService<IOpenIddictServerDispatcher>().DispatchAsync<OpenIddictServerEvents.ProcessRequestContext>(null!);

    /// <summary>Proves the legacy standalone store scope cannot impersonate the shared request boundary.</summary>
    internal async Task DispatchStandaloneAsync()
    {
        await using var scope = await ProtocolStorageScope.OpenAsync(Storage.Factory, TestContext.Current.CancellationToken);
        await DispatchFromAsync(scope.Services);
    }

    /// <summary>Creates a real native transaction in the selected provider before asking its dispatcher for admission.</summary>
    private async Task DispatchFromAsync(IServiceProvider services)
    {
        if (client)
        {
            var transaction = await services.GetRequiredService<IOpenIddictClientFactory>().CreateTransactionAsync();
            await services.GetRequiredService<IOpenIddictClientDispatcher>().DispatchAsync(new OpenIddictClientEvents.ProcessRequestContext(transaction));
        }
        else
        {
            var transaction = await services.GetRequiredService<IOpenIddictServerFactory>().CreateTransactionAsync();
            await services.GetRequiredService<IOpenIddictServerDispatcher>().DispatchAsync(new OpenIddictServerEvents.ProcessRequestContext(transaction));
        }
    }

    /// <summary>Proves a native service-created scope cannot use owned decorators without explicit activation.</summary>
    internal async Task DispatchUnownedAsync()
    {
        await using var scope = Storage.Root.CreateAsyncScope();
        await DispatchFromAsync(scope.ServiceProvider);
    }

    /// <summary>Injects a deterministic native filter outcome before the server handler can run.</summary>
    private sealed class ServerFilter(Func<bool> filter) : IOpenIddictServerHandlerFilter<OpenIddictServerEvents.ProcessRequestContext>
    {
        /// <summary>Runs through the real native server filter dispatcher.</summary>
        public ValueTask<bool> IsActiveAsync(OpenIddictServerEvents.ProcessRequestContext context) => new(filter());
    }

    /// <summary>Injects a deterministic native filter outcome before the client handler can run.</summary>
    private sealed class ClientFilter(Func<bool> filter) : IOpenIddictClientHandlerFilter<OpenIddictClientEvents.ProcessRequestContext>
    {
        /// <summary>Runs through the real native client filter dispatcher.</summary>
        public ValueTask<bool> IsActiveAsync(OpenIddictClientEvents.ProcessRequestContext context) => new(filter());
    }

    /// <summary>Exposes native server outcomes without substituting the dispatcher or context.</summary>
    private static NativeEvent Wrap(OpenIddictServerEvents.BaseRequestContext context) => new(
        () => context.CancellationToken, token => context.CancellationToken = token,
        context.HandleRequest, context.SkipRequest, () => ((OpenIddictServerEvents.BaseValidatingContext)context).Reject());

    /// <summary>Exposes native client outcomes without substituting the dispatcher or context.</summary>
    private static NativeEvent Wrap(OpenIddictClientEvents.BaseRequestContext context) => new(
        () => context.CancellationToken, token => context.CancellationToken = token,
        context.HandleRequest, context.SkipRequest, () => ((OpenIddictClientEvents.BaseValidatingContext)context).Reject());

    /// <summary>Disposes the fixture provider after the request has drained every native handler.</summary>
    public ValueTask DisposeAsync() => Storage.DisposeAsync();
}

/// <summary>Lets shared test scenarios observe each native event's real token and outcome methods.</summary>
internal sealed record NativeEvent(Func<CancellationToken> ReadToken, Action<CancellationToken> WriteToken,
    Action Handle, Action Skip, Action Reject);
