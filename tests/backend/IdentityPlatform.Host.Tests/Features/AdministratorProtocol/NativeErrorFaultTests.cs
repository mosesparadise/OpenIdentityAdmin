using System.Net;
using System.Text.Json;
using IdentityPlatform.Host.Tests.Features.Accounts;
using IdentityPlatform.Host.Tests.Features.ProtocolStorage;
using Microsoft.AspNetCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using OpenIddict.Server.AspNetCore;

namespace IdentityPlatform.Host.Tests.Features.AdministratorProtocol;

/// <summary>Exercises real native error serialization with injected fields, callbacks and mutable response substitutions.</summary>
public sealed class NativeErrorFaultTests(AdministratorRegistrationFixture fixture) : IClassFixture<AdministratorRegistrationFixture>
{
    private const string EnabledSetting = "Identity:AdministratorProtocol:Enabled";
    private const string RedirectSetting = "Identity:AdministratorProtocol:RedirectUri";
    private const string PublicKeySetting = "Identity:AdministratorProtocol:PublicKey";
    private const string Credential = "credential";
    private const string Custom = "custom";
    private const string Array = "array";
    private const string CookieMutation = "cookie";
    private const string Header = "header";
    private const string Response = "response";
    private const string CallbackMutation = "callback";
    private const string ChallengeHeader = "challenge-header";
    private const string CookieHeader = "Set-Cookie";

    private const string Callback = "https://administrator.example.test/callback";
    private const string Client = "identity-admin-bff";
    private const string TokenPath = "/connect/token";
    private const string Sentinel = "synthetic-forbidden-error-output";
    private const string ExtraHeader = "X-Forbidden-Error";
    private const string Cookie = "synthetic-cookie=forbidden";

    /// <summary>Requires the injected defect to execute, then verifies the HTTP boundary exposes only its fixed unavailable response.</summary>
    [Theory]
    [InlineData(Credential)]
    [InlineData(Custom)]
    [InlineData(Array)]
    [InlineData(CookieMutation)]
    [InlineData(Header)]
    [InlineData(Response)]
    [InlineData(CallbackMutation)]
    [InlineData(ChallengeHeader)]
    public async Task Given_NativeError_When_ResponseIsCorrupted_Then_NoUnverifiedOutputPublishes(string mutation)
    {
        var observed = 0;
        await using var host = await AccountHost.StartAsync(false, true, configureConfiguration: Configure,
            configureServices: services => AddFault(services, mutation, () => observed++));
        using var form = new FormUrlEncodedContent([new(OpenIddictConstants.Parameters.ClientId, Client)]);
        using var response = await host.Client.PostAsync(TokenPath, form, TestContext.Current.CancellationToken);
        Assert.Equal(mutation == ChallengeHeader ? 3 : 1, observed);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
        Assert.False(response.Headers.Contains(CookieHeader));
        Assert.False(response.Headers.Contains(ExtraHeader));
        Assert.Empty(response.Headers.WwwAuthenticate);
        Assert.Null(response.Headers.Location);
    }

    /// <summary>Injects finite synthetic corruption after native error creation, preserving the real pipeline and transport boundary.</summary>
    private static void AddFault(IServiceCollection services, string mutation, Action observed)
    {
        var server = services.AddOpenIddict().AddServer();
        server.AddEventHandler<OpenIddictServerEvents.ProcessErrorContext>(handler => handler
            .SetOrder(OpenIddictServerHandlers.AttachErrorParameters.Descriptor.Order + 1).UseInlineHandler(context =>
            {
                observed();
                Mutate(context, mutation);
                return ValueTask.CompletedTask;
            }));
        if (mutation == ChallengeHeader)
        {
            server.AddEventHandler<OpenIddictServerEvents.ApplyTokenResponseContext>(handler => handler
                .SetOrder(OpenIddictServerAspNetCoreHandlers.AttachWwwAuthenticateHeader<OpenIddictServerEvents.ApplyTokenResponseContext>.Descriptor.Order - 1)
                .UseInlineHandler(context => { observed(); context.Response.SetParameter(OpenIddictConstants.Parameters.AccessToken, Sentinel); return ValueTask.CompletedTask; }));
            server.AddEventHandler<OpenIddictServerEvents.ApplyTokenResponseContext>(handler => handler
                .SetOrder(OpenIddictServerAspNetCoreHandlers.AttachWwwAuthenticateHeader<OpenIddictServerEvents.ApplyTokenResponseContext>.Descriptor.Order + 1)
                .UseInlineHandler(context => { observed(); context.Response.RemoveParameter(OpenIddictConstants.Parameters.AccessToken); return ValueTask.CompletedTask; }));
        }
    }

    /// <summary>Changes one independently observable field or callback; values are disposable and never real credentials.</summary>
    private static void Mutate(OpenIddictServerEvents.ProcessErrorContext context, string mutation)
    {
        var http = context.Transaction.GetHttpRequest()!.HttpContext;
        switch (mutation)
        {
            case Credential:
                context.Response.SetParameter(OpenIddictConstants.Parameters.AccessToken, Sentinel);
                break;
            case Custom:
                context.Response.SetParameter(ExtraHeader, Sentinel);
                break;
            case Array:
                context.Response.SetParameter(OpenIddictConstants.Parameters.Error, JsonSerializer.SerializeToElement(new[] { OpenIddictConstants.Errors.InvalidClient }));
                break;
            case CookieMutation:
                http.Response.Headers.SetCookie = Cookie;
                break;
            case Header:
                http.Response.Headers[ExtraHeader] = Sentinel;
                break;
            case Response:
                context.Response = new(context.Response.GetParameters());
                break;
            case CallbackMutation:
                http.Response.OnStarting(() => { http.Response.Headers.SetCookie = Cookie; return Task.CompletedTask; });
                break;
        }
    }

    /// <summary>Uses exact production composition and public-only protocol settings without initializing or opening a database.</summary>
    private void Configure(IConfiguration configuration)
    {
        configuration[EnabledSetting] = bool.TrueString;
        configuration[RedirectSetting] = Callback;
        configuration[PublicKeySetting] = fixture.Json;
    }
}
