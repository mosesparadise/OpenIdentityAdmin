using System.Text;
using System.Text.Json;
using IdentityPlatform.Admin.Api.Features.Applications;
using IdentityPlatform.Admin.Bff.Features.Applications;
using IdentityPlatform.Admin.Bff.Features.BrowserSessions;
using IdentityPlatform.Core.Features.Applications;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Host.Tests.Features.Applications;

/// <summary>Exercises clone source preconditions and strict new-destination binding before any genuine transport or storage dependency.</summary>
public sealed class ApplicationCloneTransportTests
{
    private const string ApiPath = "/api/v1/applications/source-id/clone";
    private const string ValidTag = "valid";
    private const string Client = "new-client";
    private const string Display = "New client";
    private const string Callback = "https://client.example/callback";
    private const string Profile = "managed-web-v1";
    private const string Foreign = "foreign";
    private const string Unsupported = "unsupported";
    private const string Issuer = "https://issuer.example.test/";
    private const string Separator = "/";
    private const string Https = "https";
    private const string Post = "POST";
    private const string Host = "bff.example.test";
    private const string FetchHeader = "Sec-Fetch-Site";
    private const string SameOrigin = "same-origin";
    private const string Csrf = "X-CSRF-TOKEN";
    private const string SyntheticCsrf = "synthetic-parser-only";
    private const string JsonType = "application/json";
    private const int SourceRevision = 4;
    private const int ChangedRevision = 2;
    private const int AlternateExpiryDays = 2;
    private const int SecretBytes = 32;
    private const int Created = 201;
    private const string Source = "source-id";
    private const string Destination = "destination-id";
    private const string Origin = "https://bff.example.test";
    private const string Path = "/bff/api/v1/applications/source-id/clone";
    private const string Body = """{"confirmed":true,"clientId":"new-client","displayName":"New client","credentialExpiresAt":"2030-01-02T00:00:00Z"}""";
    private static readonly DateTimeOffset Now = new(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>Requires a source precondition and finite JSON before reaching native authentication; malformed input never creates a registration.</summary>
    [Theory]
    [InlineData(null, 428)]
    [InlineData("*", 412)]
    [InlineData(ValidTag, 401)]
    public async Task Given_CloneSourcePrecondition_When_ApiReceivesRequest_Then_AdmissionUsesExactExistingResourceSemantics(string? tag, int status)
    {
        await using var services = new ServiceCollection().AddLogging().AddProblemDetails().AddSingleton<TimeProvider>(TimeProvider.System).BuildServiceProvider();
        var http = Context(services);
        http.Request.Path = ApiPath;
        http.Features.Get<IHttpRequestFeature>()!.RawTarget = http.Request.Path;
        http.Request.Headers.IfMatch = tag == ValidTag ? ApplicationVersion.Create(Source, SourceRevision) : tag;
        http.Response.Body = new MemoryStream();
        await ApplicationCreateEndpoint.HandleCloneAsync(http, Source);
        Assert.Equal(status, http.Response.StatusCode);
        Assert.Equal("no-store", http.Response.Headers.CacheControl);
        Assert.Equal(0, http.Response.Headers.ETag.Count);
    }

    /// <summary>The BFF accepts a new revision-one disabled target rather than treating a clone as a same-resource update.</summary>
    [Fact]
    public async Task Given_ConfirmedClone_When_ProxyParsesSuccess_Then_NewDestinationAndRequestedCredentialDeadlineAreBound()
    {
        await using var services = Services();
        var request = await ApplicationMutationRequest.ReadAsync(Context(services), ApplicationProxyAction.Clone, Source, Now);
        Assert.Equal(ApiPath, request.Path);
        Assert.Equal(HttpMethod.Post, request.Method);
        var details = Details();
        var result = Parse(request, details, 1);
        Assert.Equal(Created, result.Status);
        Assert.Equal(Destination, result.Details!.Id);
        Assert.NotNull(result.Credential);
        foreach (var changed in new[]
        {
            details with { Id = Source }, details with { Enabled = true }, details with { ClientId = Foreign },
            details with { DisplayName = Foreign }, details with { Credential = new(Now, Now.AddDays(AlternateExpiryDays)) },
            details with { Editable = false }, details with { Profile = Unsupported }
        })
        {
            Assert.Throws<InvalidDataException>(() => Parse(request, changed, 1));
        }
        Assert.Throws<InvalidDataException>(() => Parse(request, details, ChangedRevision));
    }

    /// <summary>Unconfirmed or source-selected credential/configuration fields fail before a single upstream send can occur.</summary>
    [Theory]
    [InlineData("""{"confirmed":false,"clientId":"new-client","displayName":"New client","credentialExpiresAt":"2030-01-02T00:00:00Z"}""")]
    [InlineData("""{"confirmed":true,"clientId":"new-client","displayName":"New client","credentialExpiresAt":"2030-01-02T00:00:00Z","redirectUris":[]}""")]
    public async Task Given_UnconfirmedOrExtraInput_When_CloneProxyReads_Then_FiniteBadRequestIsReturned(string body)
    {
        await using var services = Services();
        var http = Context(services, body);
        var rejected = await Assert.ThrowsAsync<ApplicationProxyRejected>(() => ApplicationMutationRequest.ReadAsync(http, ApplicationProxyAction.Clone, Source, Now));
        Assert.Equal(400, rejected.Status);
    }

    /// <summary>Supplies safe synthetic response values without using implementation-created metadata as an oracle.</summary>
    private static ApplicationDetails Details() => new(Destination, Client, Display, [Callback],
        Profile, true, new(Now, Now.AddDays(1)), Now.AddMinutes(1), false);

    /// <summary>Checks the actual response parser with a deterministic nonproduction credential, without printing it on failure.</summary>
    private static ApplicationProxyResult Parse(ApplicationMutationRequest request, ApplicationDetails details, long revision) =>
        ApplicationProxyResult.Parse(JsonSerializer.SerializeToUtf8Bytes(new ApplicationCredentialOutput(details, Convert.ToBase64String(new byte[SecretBytes])), ApplicationJson.Options),
            ApplicationVersion.Create(details.Id, revision), request, Created);

    /// <summary>Provides only origin configuration, proving parsing is independent of bearer/session authorization.</summary>
    private static ServiceProvider Services() => new ServiceCollection().AddSingleton(new BrowserSessionDestinations(Issuer, Origin + Separator)).BuildServiceProvider();

    /// <summary>Builds exact framing with a synthetic parser-only antiforgery header; genuine proof validation belongs to HTTP integration tests.</summary>
    private static DefaultHttpContext Context(IServiceProvider services, string body = Body)
    {
        var http = new DefaultHttpContext { RequestServices = services };
        http.Request.Scheme = Https;
        http.Request.Method = Post;
        http.Request.Host = new HostString(Host);
        http.Request.Path = Path;
        http.Features.Get<IHttpRequestFeature>()!.RawTarget = Path;
        http.Request.Headers.Origin = Origin;
        http.Request.Headers[FetchHeader] = SameOrigin;
        http.Request.Headers[Csrf] = SyntheticCsrf;
        http.Request.Headers.IfMatch = ApplicationVersion.Create(Source, SourceRevision);
        http.Request.ContentType = JsonType;
        var bytes = Encoding.UTF8.GetBytes(body);
        http.Request.ContentLength = bytes.Length;
        http.Request.Body = new MemoryStream(bytes);
        return http;
    }
}
