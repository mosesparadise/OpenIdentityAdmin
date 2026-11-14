using System.Net;
using System.Text;
using IdentityPlatform.Sample.Client.Features.ResourceAccess;

namespace IdentityPlatform.Host.Tests.Features.Samples;

/// <summary>Tests finite explicit resource calls against adversarial bodies and fixed private request destinations.</summary>
public sealed class SampleResourceTests
{
    private const string SyntheticBearer = "synthetic-bearer";
    private const string SyntheticSubject = "synthetic";
    private const string SubSyntheticEmailTestExampleInvalidEmailVerifiedText = "{\"sub\":\"synthetic\",\"email\":\"test@example.invalid\",\"email_verified\":false}";
    private const string RemoteErrorCanary = "private-remote-error";
    private const string OversizedScenario = "oversized";
    private const int OversizedBodyBytes = 4097;
    private const string SubSyntheticText = "{\"sub\":\"synthetic\"}";
    private const string CompressedEncoding = "gzip";
    private const string HtmlScenario = "html";
    private const string HtmlMediaType = "text/html";
    private const string JsonMediaType = "application/json";

    /// <summary>Unspecified Content-Length cannot bypass the actual read limit, including an otherwise valid JSON prefix.</summary>
    [Fact]
    public async Task Given_StreamWithoutDeclaredLength_When_BodyExceedsLimit_Then_ActualBytesAreRejected()
    {
        using var content = new UnboundedContent();
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(JsonMediaType);
        Assert.Null(content.Headers.ContentLength);
        using var handler = new ResponseHandler(() => new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        using var client = new HttpClient(handler);
        await Assert.ThrowsAsync<InvalidOperationException>(() => new SampleResources(client).ProfileAsync(SyntheticBearer, SyntheticSubject, TestContext.Current.CancellationToken));
        Assert.Equal(1, handler.Calls);
    }

    /// <summary>Exposes a real response stream while deliberately supplying no declared content length.</summary>
    private sealed class UnboundedContent : HttpContent
    {
        private readonly byte[] body = Encoding.UTF8.GetBytes(SubSyntheticText.PadRight(OversizedBodyBytes));

        /// <summary>Leaves framing length unspecified to exercise the bounded streaming reader.</summary>
        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }

        /// <summary>Supplies body bytes if the HTTP infrastructure elects to serialize the content.</summary>
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => stream.WriteAsync(body).AsTask();

        /// <summary>Provides the unbuffered read path selected by ResponseHeadersRead.</summary>
        protected override Task<Stream> CreateContentReadStreamAsync() => Task.FromResult<Stream>(new MemoryStream(body, writable: false));
    }

    /// <summary>Only a unique exact subject and optional typed email pair may cross into presentation.</summary>
    [Theory]
    [InlineData("{\"sub\":\"other\"}")]
    [InlineData("{\"sub\":\"synthetic\",\"sub\":\"synthetic\"}")]
    [InlineData("{\"sub\":\"synthetic\",\"role\":\"admin\"}")]
    [InlineData("{\"sub\":\"synthetic\",\"email\":\"test@example.invalid\"}")]
    [InlineData("{\"sub\":\"synthetic\",\"email\":\"test@example.invalid\",\"email_verified\":\"false\"}")]
    [InlineData("[]")]
    [InlineData("not-json")]
    public async Task Given_InvalidProfile_When_CurrentProfileIsFetched_Then_NoIdentityProjectionIsReturned(string body)
    {
        using var client = new HttpClient(new ResponseHandler(() => Json(body)));
        var resources = new SampleResources(client);
        await Assert.ThrowsAnyAsync<Exception>(() => resources.ProfileAsync(SyntheticBearer, SyntheticSubject, TestContext.Current.CancellationToken));
    }

    /// <summary>False email verification remains false and all credentials travel only in the fixed HTTPS header-bearer request.</summary>
    [Fact]
    public async Task Given_CurrentProfile_When_Fetched_Then_FalseVerificationAndExactSubjectArePreserved()
    {
        using var handler = new ResponseHandler(() => Json(SubSyntheticEmailTestExampleInvalidEmailVerifiedText));
        using var client = new HttpClient(handler);
        var result = await new SampleResources(client).ProfileAsync(SyntheticBearer, SyntheticSubject, TestContext.Current.CancellationToken);
        Assert.Equal("synthetic", result.Subject);
        Assert.False(result.EmailVerified);
        Assert.Equal("test@example.invalid", result.Email);
        Assert.Equal("https://issuer.dev.localhost:17443/connect/userinfo", handler.Destination);
        Assert.Equal("Bearer synthetic-bearer", handler.Authorization);
        Assert.Equal(1, handler.Calls);
    }

    /// <summary>Current denial returns a fresh empty projection, never a cached prior profile or reflected remote error.</summary>
    [Theory]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(302)]
    [InlineData(500)]
    public async Task Given_RemoteFailure_When_Fetched_Then_OnlyFiniteDenialIsShownWithoutRetry(int status)
    {
        using var handler = new ResponseHandler(() => new HttpResponseMessage((HttpStatusCode)status) { Content = new StringContent(RemoteErrorCanary) });
        using var client = new HttpClient(handler);
        var result = await new SampleResources(client).ProfileAsync(SyntheticBearer, SyntheticSubject, TestContext.Current.CancellationToken);
        Assert.Null(result.Subject);
        Assert.Null(result.Email);
        Assert.DoesNotContain("private-remote-error", result.Message, StringComparison.Ordinal);
        Assert.Equal(1, handler.Calls);
    }

    /// <summary>Compressed, oversized actual bytes and non-JSON outputs cannot reach projection parsing.</summary>
    [Theory]
    [InlineData("gzip")]
    [InlineData("oversized")]
    [InlineData("html")]
    public async Task Given_HostileResponse_When_Fetched_Then_TransportBoundsRejectIt(string scenario)
    {
        using var client = new HttpClient(new ResponseHandler(() =>
        {
            var response = Json(scenario == OversizedScenario ? new string(' ', OversizedBodyBytes) : SubSyntheticText);
            if (scenario == CompressedEncoding)
            {
                response.Content.Headers.ContentEncoding.Add(CompressedEncoding);
            }
            if (scenario == HtmlScenario)
            {
                response.Content.Headers.ContentType!.MediaType = HtmlMediaType;
            }
            return response;
        }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => new SampleResources(client).ProfileAsync(SyntheticBearer, SyntheticSubject, TestContext.Current.CancellationToken));
    }

    /// <summary>Only the synthetic orders shape is accepted from the fixed API resource destination.</summary>
    [Theory]
    [InlineData("{\"orders\":[{\"id\":\"sample-order-001\",\"status\":\"Ready\"}]}", true)]
    [InlineData("{\"orders\":[{\"id\":\"other\",\"status\":\"Ready\"}]}", false)]
    public async Task Given_OrdersResponse_When_Fetched_Then_OnlyFiniteContractIsAccepted(string body, bool accepted)
    {
        using var handler = new ResponseHandler(() => Json(body));
        using var client = new HttpClient(handler);
        var operation = new SampleResources(client).OrdersAsync(SyntheticBearer, TestContext.Current.CancellationToken);
        if (accepted)
        {
            Assert.Contains("sample-order-001", (await operation).Message, StringComparison.Ordinal);
        }
        else
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => operation);
        }
        Assert.Equal("https://orders.dev.localhost:17447/orders", handler.Destination);
        Assert.Equal(1, handler.Calls);
    }

    /// <summary>Builds a UTF8 JSON response under test control.</summary>
    private static HttpResponseMessage Json(string body) => new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, JsonMediaType) };

    /// <summary>Records only synthetic request expectations and returns one deterministic response without network activity.</summary>
    private sealed class ResponseHandler(Func<HttpResponseMessage> response) : HttpMessageHandler
    {
        internal string? Destination
        {
            get; private set;
        }
        internal string? Authorization
        {
            get; private set;
        }
        internal int Calls
        {
            get; private set;
        }

        /// <summary>Observes the actual emitted request, including cancellation and the absence of caller-controlled destinations.</summary>
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls++;
            Destination = request.RequestUri!.AbsoluteUri;
            Authorization = request.Headers.Authorization?.ToString();
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Null(request.Content);
            return Task.FromResult(response());
        }
    }
}
