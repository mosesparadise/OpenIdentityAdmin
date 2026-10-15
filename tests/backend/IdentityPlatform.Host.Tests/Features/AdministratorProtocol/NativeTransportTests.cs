using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using OpenIddict.Client;
using OpenIddict.Client.SystemNetHttp;

namespace IdentityPlatform.Host.Tests.Features.AdministratorProtocol;

/// <summary>Exercises the pinned native HTTP factory using an in-process failing transport, without a protocol exchange.</summary>
public sealed class NativeTransportTests
{
    private const string Issuer = "https://issuer.example/";
    private const string Registration = "dependency-probe";
    private const string ClientName = "OpenIddict.Client.SystemNetHttp:dependency-probe";
    private const string ProtocolPath = "connect/";
    private const int HttpSeconds = 10;
    /// <summary>Proves both retry mechanisms can be removed and that failed mutations are sent exactly once.</summary>
    [Theory]
    [InlineData(false, "par")]
    [InlineData(false, "token")]
    [InlineData(true, "par")]
    [InlineData(true, "token")]
    public async Task Given_NativeFactoryWithoutRetries_When_MutationResponseFails_Then_SendsExactlyOnce(bool lostResponse, string endpoint)
    {
        var services = new ServiceCollection().AddLogging();
        var witness = new NativeTransportWitness(lostResponse);
        services.AddSingleton(witness);
        services.AddSingleton<IHttpMessageHandlerBuilderFilter, NativeTransportFilter>();
        services.AddOpenIddict().AddClient(client => client.UseSystemNetHttp(http =>
        {
            http.Configure(options => { options.HttpErrorPolicy = null; options.HttpResiliencePipeline = null; });
            http.ConfigureHttpClient(client => client.Timeout = TimeSpan.FromSeconds(HttpSeconds));
            http.ConfigureHttpClientHandler(handler => handler.AllowAutoRedirect = false);
        }));
        await using var provider = services.BuildServiceProvider();
        var previous = OpenIddictClientSystemNetHttpContext.Current;
        OpenIddictClientSystemNetHttpContext.Current = new()
        {
            Registration = new OpenIddictClientRegistration { RegistrationId = Registration, Issuer = new Uri(Issuer) }
        };
        try
        {
            using var http = provider.GetRequiredService<IHttpClientFactory>().CreateClient(ClientName);
            Assert.Equal(TimeSpan.FromSeconds(HttpSeconds), http.Timeout);
            Assert.True(witness.RedirectsDisabled);
            using var body = new FormUrlEncodedContent([]);
            await AssertOutcomeAsync(http, body, endpoint, lostResponse);
            Assert.Equal(1, witness.Count);
        }
        finally { OpenIddictClientSystemNetHttpContext.Current = previous; }
    }

    /// <summary>Distinguishes an unavailable response from response loss without accepting either as protocol success.</summary>
    private static async Task AssertOutcomeAsync(HttpClient http, HttpContent body, string endpoint, bool lostResponse)
    {
        var uri = new Uri(Issuer + ProtocolPath + endpoint);
        if (lostResponse)
        {
            await Assert.ThrowsAsync<HttpRequestException>(() => http.PostAsync(uri, body, TestContext.Current.CancellationToken));
            return;
        }
        using var response = await http.PostAsync(uri, body, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }
}

/// <summary>Retains only finite transport observations; no request body or credentials are captured.</summary>
internal sealed class NativeTransportWitness(bool lostResponse)
{
    internal bool LostResponse { get; } = lostResponse;
    internal int Count
    {
        get; set;
    }
    internal bool RedirectsDisabled
    {
        get; set;
    }
}

/// <summary>Observes native configuration before replacing only the final network transport with a synthetic failure.</summary>
internal sealed class NativeTransportFilter(NativeTransportWitness witness) : IHttpMessageHandlerBuilderFilter
{
    /// <summary>Preserves the real native factory handler chain and prevents any external request from being sent.</summary>
    public Action<HttpMessageHandlerBuilder> Configure(Action<HttpMessageHandlerBuilder> next) => builder =>
    {
        next(builder);
        witness.RedirectsDisabled = builder.PrimaryHandler is HttpClientHandler { AllowAutoRedirect: false };
        builder.PrimaryHandler.Dispose();
        builder.PrimaryHandler = new NativeFailureTransport(witness);
    };
}

/// <summary>Counts calls from the native handler chain and simulates an unavailable or lost response.</summary>
internal sealed class NativeFailureTransport(NativeTransportWitness witness) : HttpMessageHandler
{
    /// <summary>Fails every synthetic mutation, allowing a retry to be detected by its additional transport invocation.</summary>
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        const string Failure = "Synthetic response loss";
        witness.Count++;
        return witness.LostResponse ? Task.FromException<HttpResponseMessage>(new HttpRequestException(Failure)) :
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
    }
}
