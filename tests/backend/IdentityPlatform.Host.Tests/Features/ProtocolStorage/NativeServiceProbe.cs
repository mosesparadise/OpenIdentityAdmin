using System.Reflection;
using IdentityPlatform.Admin.Bff.Features.Authentication.Ownership;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using OpenIddict.Client;

namespace IdentityPlatform.Host.Tests.Features.ProtocolStorage;

/// <summary>Exercises pinned native service-created scopes with synthetic transport events and real native factories/dispatchers.</summary>
internal sealed class NativeServiceProbe : IAsyncDisposable
{
    private const string PushedMethod = "SendPushedAuthorizationRequestAsync";
    private const string TokenMethod = "SendTokenRequestAsync";
    private const string TransportAddress = "https://synthetic.invalid/transport";
    private const string KeysMethod = "GetSecurityKeysAsync";
    private const string ConfigurationMethod = "GetConfigurationAsync";
    private const string MetadataAddress = "https://synthetic.invalid/metadata";
    private const string ChallengeNonce = "synthetic-nonce";

    internal StorageRequestProbe Storage
    {
        get;
    }
    internal List<ProtocolStorageOwner> Owners { get; } = [];
    internal Func<ValueTask> Event { get; set; } = () => ValueTask.CompletedTask;
    internal OpenIddictClientService Service => Storage.Root.GetRequiredService<OpenIddictClientService>();

    /// <summary>Installs the dormant service facade without changing the application's real scope factory.</summary>
    internal NativeServiceProbe(Action<IServiceCollection>? configure = null) => Storage = new(services =>
    {
        Configure(services);
        configure?.Invoke(services);
    });

    /// <summary>Runs the actual internal PAR/token method; reflection is confined to this pinned-version test fixture.</summary>
    internal ValueTask<OpenIddictResponse> SendAsync(bool pushed, CancellationToken token = default)
    {
        var name = pushed ? PushedMethod : TokenMethod;
        var method = typeof(OpenIddictClientService).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (ValueTask<OpenIddictResponse>)method.Invoke(Service, [new OpenIddictClientRegistration(), new OpenIddictConfiguration(),
            new OpenIddictRequest(), new Uri(TransportAddress), null, null, token])!;
    }

    /// <summary>Runs actual native metadata/JWKS scope creation with synthetic extraction under explicit ownership.</summary>
    internal async Task ReadMetadataAsync(bool keys, CancellationToken token)
    {
        var name = keys ? KeysMethod : ConfigurationMethod;
        var method = typeof(OpenIddictClientService).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!;
        var result = method.Invoke(Service, [new OpenIddictClientRegistration(), new Uri(MetadataAddress), token]);
        if (keys)
        {
            await (ValueTask<JsonWebKeySet>)result!;
        }
        else
        {
            await (ValueTask<OpenIddictConfiguration>)result!;
        }
    }

    /// <summary>Retains four native transport stages while replacing only actual network I/O with synthetic handlers.</summary>
    private void Configure(IServiceCollection services)
    {
        services.AddOptions<OpenIddictClientOptions>().Configure(options =>
        {
            options.Handlers.Clear();
            Add<OpenIddictClientEvents.PrepareTokenRequestContext>(options);
            Add<OpenIddictClientEvents.ApplyTokenRequestContext>(options);
            Add<OpenIddictClientEvents.ExtractTokenResponseContext>(options, context => context.Response = new());
            Add<OpenIddictClientEvents.HandleTokenResponseContext>(options);
            Add<OpenIddictClientEvents.PreparePushedAuthorizationRequestContext>(options);
            Add<OpenIddictClientEvents.ApplyPushedAuthorizationRequestContext>(options);
            Add<OpenIddictClientEvents.ExtractPushedAuthorizationResponseContext>(options, context => context.Response = new());
            Add<OpenIddictClientEvents.HandlePushedAuthorizationResponseContext>(options);
            Add<OpenIddictClientEvents.PrepareConfigurationRequestContext>(options);
            Add<OpenIddictClientEvents.ApplyConfigurationRequestContext>(options);
            Add<OpenIddictClientEvents.ExtractConfigurationResponseContext>(options, context => context.Response = new());
            Add<OpenIddictClientEvents.HandleConfigurationResponseContext>(options, context => context.Configuration = new());
            Add<OpenIddictClientEvents.PrepareJsonWebKeySetRequestContext>(options);
            Add<OpenIddictClientEvents.ApplyJsonWebKeySetRequestContext>(options);
            Add<OpenIddictClientEvents.ExtractJsonWebKeySetResponseContext>(options, context => context.Response = new());
            Add<OpenIddictClientEvents.HandleJsonWebKeySetResponseContext>(options);
            Add<OpenIddictClientEvents.ProcessChallengeContext>(options, context => context.Nonce = ChallengeNonce);
        });
        services.AddOwnedClientDispatcher().AddOwnedClientServiceScopes();
    }

    /// <summary>Records the actual ambient child and checks that native dispatch replaces the caller token with owned cancellation.</summary>
    private void Add<T>(OpenIddictClientOptions options, Action<T>? initialize = null) where T : OpenIddictClientEvents.BaseContext
    {
        options.Handlers.Add(OpenIddictClientHandlerDescriptor.CreateBuilder<T>().UseInlineHandler(async context =>
        {
            var owner = ProtocolStorageAmbient.Current!.Scope.Owner;
            lock (Owners)
            {
                Owners.Add(owner);
            }
            Assert.True(context.CancellationToken.CanBeCanceled);
            Assert.NotEqual(owner.RequestToken, context.CancellationToken);
            initialize?.Invoke(context);
            await Event();
        }).Build());
    }

    /// <summary>Disposes the fixture only after every service-owned scope has drained.</summary>
    public ValueTask DisposeAsync() => Storage.DisposeAsync();
}
