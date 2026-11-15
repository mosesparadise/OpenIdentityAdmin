using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using IdentityPlatform.Admin.Bff.Features.Applications;
using IdentityPlatform.Admin.Bff.Features.BrowserSessions;
using IdentityPlatform.Core.Features.Applications;
using static IdentityPlatform.Host.Tests.Features.Applications.ApplicationRetirementBoundaryTests;

namespace IdentityPlatform.Host.Tests.Features.Applications;

/// <summary>Exercises the actual single-send retirement transport with independent finite downstream framing and acceptance substitutions.</summary>
public sealed class ApplicationRetirementTransportTests
{
    private const string StatusDefect = "status";
    private const string MediaDefect = "media";
    private const string EncodingDefect = "encoding";
    private const string LengthDefect = "length";
    private const string OversizeDefect = "oversize";
    private const string EtagDefect = "etag";
    private const string LocationDefect = "location";
    private const string AbsoluteLocationDefect = "absolute-location";
    private const string Target = "https://api.example.test/";
    private const string Job = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee";
    private const string OtherJob = "ffffffff-bbbb-cccc-dddd-eeeeeeeeeeee";
    private const string Foreign = "other-application";
    private const string JsonType = "application/json";
    private const string Plain = "text/plain";
    private const string Gzip = "gzip";
    private const string Bearer = "Bearer";
    private const string Credential = "synthetic-parser-only";
    private const string IfMatch = "If-Match";
    private const string Queued = "queued";
    private const string Succeeded = "succeeded";
    private const string None = "none";
    private const string Zero = "0";
    private const string InvalidCount = "01";
    private const string JobPath = "/api/v1/applications/" + Source + "/grant-revocations/" + Job;
    private const long ChangedJobRevision = 7;
    private const int Accepted = 202;

    /// <summary>Allows exact replay to return later committed status while preserving original terminal application identity and one fixed request.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_BoundAcceptance_When_OriginalOrAdvancedJobReturns_Then_OneSendPublishesExactTerminalVersion(bool advanced)
    {
        var accepted = Acceptance();
        if (advanced)
        {
            accepted = accepted with
            {
                Job = accepted.Job with
                {
                    State = Succeeded,
                    Version = ApplicationVersion.Create(Job, ChangedJobRevision),
                    UpdatedAt = Now.AddSeconds(1)
                }
            };
        }
        var result = await SendAsync(Response(accepted));
        Assert.Equal(Accepted, result.Status);
        Assert.Equal(accepted, result.Acceptance);
    }

    /// <summary>Every destructive binding is checked independently, including successor revision, original timestamp and the exact job resource.</summary>
    [Fact]
    public async Task Given_SubstitutedAcceptance_When_TransportReads_Then_NoUnboundTerminalResultIsPublished()
    {
        var value = Acceptance();
        foreach (var changed in new[]
        {
            value with { ApplicationId = Foreign }, value with { RequestKey = OtherJob },
            value with { ApplicationVersion = ApplicationVersion.Create(Source, SourceRevision) },
            value with { ApplicationVersion = ApplicationVersion.Create(Foreign, SourceRevision + 1) },
            value with { RetiredAt = Now.AddSeconds(1) }, value with { RetiredAt = Now.ToOffset(TimeSpan.FromHours(1)) },
            value with { RetiredAt = Now.AddTicks(1) }, value with { Job = null! },
            value with { Job = value.Job with { ApplicationId = Foreign } },
            value with { Job = value.Job with { Id = OtherJob } },
            value with { Job = value.Job with { AuthorizationCount = InvalidCount } }
        })
        {
            await Assert.ThrowsAsync<InvalidDataException>(() => SendAsync(Response(changed)));
        }
    }

    /// <summary>Wrong HTTP status, media/encoding/length, job ETag and location cannot masquerade as an accepted bounded retirement.</summary>
    [Theory]
    [InlineData("status")]
    [InlineData("media")]
    [InlineData("encoding")]
    [InlineData("length")]
    [InlineData("oversize")]
    [InlineData("etag")]
    [InlineData("location")]
    [InlineData("absolute-location")]
    public async Task Given_InvalidResponseFraming_When_TransportReads_Then_PublicationFailsClosed(string defect)
    {
        using var response = Response(Acceptance());
        switch (defect)
        {
            case StatusDefect:
                response.StatusCode = HttpStatusCode.OK;
                break;
            case MediaDefect:
                response.Content.Headers.ContentType = new(Plain);
                break;
            case EncodingDefect:
                response.Content.Headers.ContentEncoding.Add(Gzip);
                break;
            case LengthDefect:
                response.Content.Headers.ContentLength = 1;
                break;
            case OversizeDefect:
                response.Content.Headers.ContentLength = ApplicationProjection.MaximumResponseBytes + 1;
                break;
            case EtagDefect:
                response.Headers.ETag = new(ApplicationVersion.Create(Source, SourceRevision + 1));
                break;
            case LocationDefect:
                response.Headers.Location = new(JobPath + OtherJob, UriKind.Relative);
                break;
            case AbsoluteLocationDefect:
                response.Headers.Location = new(new Uri(Target), JobPath);
                break;
        }
        await Assert.ThrowsAsync<InvalidDataException>(() => SendAsync(response));
    }

    /// <summary>Finite downstream denials propagate their status without parsing or reflecting untrusted response content.</summary>
    [Theory]
    [InlineData(400)]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(404)]
    [InlineData(409)]
    [InlineData(412)]
    [InlineData(428)]
    public async Task Given_DownstreamDenial_When_TransportReturns_Then_OnlyFiniteStatusSurvives(int status)
    {
        var result = await SendAsync(new((HttpStatusCode)status));
        Assert.Equal(status, result.Status);
        Assert.Null(result.Acceptance);
    }

    /// <summary>Builds independent canonical terminal and initial-job metadata with distinct strong resource versions.</summary>
    private static ApplicationRetirementAcceptance Acceptance() => new(Source, ApplicationVersion.Create(Source, SourceRevision + 1), Now, RequestIdentifier,
        new(Job, Source, ApplicationVersion.Create(Job, 1), Queued, None, Zero, Zero, Now, Now, Now.AddMinutes(1)));

    /// <summary>Serializes the real public contract and applies exact job response headers.</summary>
    private static HttpResponseMessage Response(ApplicationRetirementAcceptance value)
    {
        var response = new HttpResponseMessage(HttpStatusCode.Accepted)
        {
            Content = new ByteArrayContent(JsonSerializer.SerializeToUtf8Bytes(value, ApplicationJson.Options))
        };
        response.Content.Headers.ContentType = new(JsonType);
        response.Headers.ETag = new(value.Job?.Version ?? ApplicationVersion.Create(Job, 1));
        response.Headers.Location = new(JobPath, UriKind.Relative);
        return response;
    }

    /// <summary>Uses one original captured server credential and checks send count even when downstream parsing throws.</summary>
    private static async Task<ApplicationRetirementProxyResult> SendAsync(HttpResponseMessage response)
    {
        var request = await RequestAsync();
        using var handler = new Handler(response);
        using var client = new HttpClient(handler);
        try
        {
            return await ApplicationRetirementTransport.SendAsync(client, new(Target), BrowserAccessCredential.Capture(Credential, Now.AddMinutes(1), Now)!, request, TestContext.Current.CancellationToken);
        }
        finally { Assert.Equal(1, handler.Sends); }
    }

    /// <summary>Checks fixed routing, original precondition and canonical key without real credentials or an alternate success implementation.</summary>
    private sealed class Handler(HttpResponseMessage response) : HttpMessageHandler
    {
        internal int Sends
        {
            get; private set;
        }

        /// <summary>Returns the supplied independent wire response only after checking the actual immutable outbound request.</summary>
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Sends++;
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal(new Uri(new Uri(Target), ApiPath), request.RequestUri);
            Assert.Equal(new AuthenticationHeaderValue(Bearer, Credential), request.Headers.Authorization);
            Assert.Equal(RequestIdentifier, Assert.Single(request.Headers.GetValues(KeyHeader)));
            Assert.Equal(ApplicationVersion.Create(Source, SourceRevision), Assert.Single(request.Headers.GetValues(IfMatch)));
            return Task.FromResult(response);
        }
    }
}
