using System.Text;
using System.Text.Encodings.Web;
using IdentityPlatform.AuthServer.Features.Protocol.Ownership;
using IdentityPlatform.AuthServer.Features.Protocol.Token;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using OpenIddict.Server.AspNetCore;

namespace IdentityPlatform.Host.Tests.Features.ProtocolStorage;

/// <summary>Runs installed HTTP extraction through the first post-selection manager resolution without opening a database.</summary>
internal sealed class NativeSelectionProbe : IAsyncDisposable
{
    private const string AuthorizePath = "/authorize";
    private const string TokenPath = "/token";
    private const string Host = "selection.example.test";
    private const string Https = "https";
    private const string QueryPrefix = "?";
    private const string FormMediaType = "application/x-www-form-urlencoded";
    private const string BasicPrefix = "Basic ";
    private const string SyntheticSecret = ":synthetic-secret";
    internal StorageRequestProbe Storage
    {
        get;
    }
    internal bool? Owned
    {
        get; private set;
    }
    internal string? Error
    {
        get; private set;
    }

    /// <summary>Uses the real native request/extraction dispatchers, filters, Basic parser and application consistency guard.</summary>
    internal NativeSelectionProbe() => Storage = new StorageRequestProbe(Configure);

    /// <summary>Sends an HTTPS form or query through the installed ASP.NET request handler and retains its native error.</summary>
    internal Task<bool> RunAsync(bool tokenEndpoint, string values, string? basicClient = null)
    {
        var request = new ProtocolStorageRequest(Storage.Clock, TestContext.Current.CancellationToken);
        return request.RunAsync(Storage.Factory, async (scope, cancellation) =>
        {
            var http = new DefaultHttpContext { RequestServices = scope.Services, RequestAborted = cancellation };
            http.Request.Scheme = Https;
            http.Request.Host = new HostString(Host);
            http.Request.Path = tokenEndpoint ? TokenPath : AuthorizePath;
            http.Response.Body = new MemoryStream();
            if (tokenEndpoint)
            {
                http.Request.Method = HttpMethods.Post;
                http.Request.ContentType = FormMediaType;
                http.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(values));
                http.Request.ContentLength = http.Request.Body.Length;
            }
            else
            {
                http.Request.Method = HttpMethods.Get;
                http.Request.QueryString = new QueryString(QueryPrefix + values);
            }
            if (basicClient is not null)
            {
                http.Request.Headers.Authorization = BasicPrefix + Convert.ToBase64String(Encoding.ASCII.GetBytes(basicClient + SyntheticSecret));
            }
            var handler = ActivatorUtilities.CreateInstance<OpenIddictServerAspNetCoreHandler>(scope.Services);
            await handler.InitializeAsync(new AuthenticationScheme(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme, null, handler.GetType()), http);
            var handled = await handler.HandleRequestAsync();
            Error = http.Features.Get<OpenIddictServerAspNetCoreFeature>()!.Transaction!.Response?.Error;
            return handled;
        });
    }

    /// <summary>Stops immediately after native extraction and selection, resolving the real manager only after the mode is frozen.</summary>
    private void Configure(IServiceCollection services)
    {
        services.AddSingleton(UrlEncoder.Default);
        services.AddOpenIddict().AddCore(options => options.UseEntityFrameworkCore());
        var builder = services.AddOpenIddict().AddServer().AllowAuthorizationCodeFlow()
            .SetAuthorizationEndpointUris(AuthorizePath).SetTokenEndpointUris(TokenPath)
            .AddEphemeralEncryptionKey().AddEphemeralSigningKey().AddTokenExtraction().AddServerStoreSelection();
        builder.UseAspNetCore();
        builder.AddEventHandler<OpenIddictServerEvents.ProcessRequestContext>(handler => handler
            .SetOrder(OpenIddictServerHandlers.Exchange.ExtractTokenRequest.Descriptor.Order + ServerStoreSelection.AfterExtraction + 1)
            .UseInlineHandler(context =>
            {
                var scope = ProtocolStorageAmbient.Current!.Scope;
                Owned = scope.Services.GetRequiredService<ProtocolStorageSelection>().ResolveOwned();
                scope.Services.GetRequiredService<IOpenIddictTokenManager>();
                context.HandleRequest();
                return ValueTask.CompletedTask;
            }));
    }

    /// <summary>Disposes native services after the original request has drained its scope.</summary>
    public ValueTask DisposeAsync() => Storage.DisposeAsync();
}
