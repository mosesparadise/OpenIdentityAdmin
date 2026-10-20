using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Options;
using OpenIddict.Client;
using OpenIddict.Client.SystemNetHttp;

namespace IdentityPlatform.Host.Tests.Features.AdministratorClient;

/// <summary>Reproduces native HTTP-factory postconfiguration with a test-only interception layer and no database or network access.</summary>
public sealed class NativeClientFactoryTests(NativeClientCredentialFixture fixture) : IClassFixture<NativeClientCredentialFixture>
{
    /// <summary>Native postconfiguration rejects replacing its primary handler; a delegating interception preserves real transport policy and one send.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_NativeHttpFactory_When_TestTransportIsInstalled_Then_OnlyDelegatingInterceptionPreservesNativeConfiguration(bool replacePrimary)
    {
        using var credentials = NativeClientProfileTests.Load(fixture);
        using var sink = new NetworkSink();
        HttpClientHandler? transport = null;
        var builders = 0;
        await using var provider = NativeClientCompositionTests.Create(credentials, services => services.ConfigureAll<HttpClientFactoryOptions>(options =>
        {
            options.HandlerLifetime = Timeout.InfiniteTimeSpan;
            options.HttpMessageHandlerBuilderActions.Add(builder =>
            {
                builders++;
                transport = Assert.IsType<HttpClientHandler>(builder.PrimaryHandler);
                if (replacePrimary)
                {
                    transport.Dispose();
                    builder.PrimaryHandler = sink;
                }
                else
                {
                    builder.AdditionalHandlers.Add(sink);
                }
            });
        }));
        var options = provider.GetRequiredService<IOptionsMonitor<OpenIddictClientOptions>>().CurrentValue;
        var context = new OpenIddictClientEvents.PrepareConfigurationRequestContext(new OpenIddictClientTransaction
        {
            Options = options,
            Registration = Assert.Single(options.Registrations)
        });
        var handler = new OpenIddictClientSystemNetHttpHandlers.CreateHttpClient<OpenIddictClientEvents.PrepareConfigurationRequestContext>(
            provider.GetRequiredService<IHttpClientFactory>());
        if (replacePrimary)
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => handler.HandleAsync(context).AsTask());
            Assert.Equal(0, sink.Sends);
        }
        else
        {
            await handler.HandleAsync(context);
            using var client = context.Transaction.GetHttpClient();
            using var response = await client!.GetAsync(NativeClientProfileTests.Issuer, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(1, sink.Sends);
            Assert.False(transport!.AllowAutoRedirect);
            Assert.False(transport.UseCookies);
        }
        Assert.Equal(1, builders);
    }

    /// <summary>Intercepts network execution without replacing the native HttpClientHandler or its postconfiguration.</summary>
    private sealed class NetworkSink : DelegatingHandler
    {
        internal int Sends
        {
            get; private set;
        }

        /// <summary>Returns a finite synthetic response without inspecting or logging any request material.</summary>
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Sends++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}
