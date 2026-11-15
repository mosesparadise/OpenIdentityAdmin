using System.Text;
using IdentityPlatform.Admin.Api.Features.Applications;
using IdentityPlatform.Admin.Bff.Features.Applications;
using IdentityPlatform.Admin.Bff.Features.BrowserSessions;
using IdentityPlatform.Core.Features.Applications;
using IdentityPlatform.Infrastructure.Features.Applications;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Host.Tests.Features.Applications;

/// <summary>Checks the retirement-specific strict input boundary before transport or authenticated persistence can be reached.</summary>
public sealed class ApplicationRetirementBoundaryTests
{
    internal const string Source = "retirement-source";
    internal const string RequestIdentifier = "01234567-89ab-cdef-0123-456789abcdef";
    internal const string KeyHeader = "Idempotency-Key";
    internal const string ApiPath = "/api/v1/applications/" + Source + "/retirement";
    private const string Path = "/bff" + ApiPath;
    private const string Body = """{"reason":"security","confirm":true}""";
    private const string Issuer = "https://issuer.example.test/";
    private const string Origin = "https://bff.example.test";
    private const string Host = "bff.example.test";
    private const string Separator = "/";
    private const string Https = "https";
    private const string Post = "POST";
    private const string FetchHeader = "Sec-Fetch-Site";
    private const string SameOrigin = "same-origin";
    private const string Csrf = "X-CSRF-TOKEN";
    private const string SyntheticCsrf = "synthetic-parser-only";
    private const string JsonType = "application/json";
    private const string NoStore = "no-store";
    private const string Valid = "valid";
    private const string Reason = "security";
    private const string Wildcard = "*";
    internal const int SourceRevision = 4;
    internal static readonly DateTimeOffset Now = new(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>Missing, zero, aliased and repeated UUID keys fail at both independent admission boundaries.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData("01234567-89AB-CDEF-0123-456789ABCDEF")]
    [InlineData("0123456789abcdef0123456789abcdef")]
    [InlineData("01234567-89ab-cdef-0123-456789abcdef,01234567-89ab-cdef-0123-456789abcdef")]
    public Task Given_NoncanonicalRetirementKey_When_EitherBoundaryReads_Then_BadRequestPrecedesAnyAuthority(string? key) =>
        RequireAsync(http => http.Request.Headers[KeyHeader] = key, StatusCodes.Status400BadRequest);

    /// <summary>Retirement cannot borrow a continuation job version, free text, duplicate confirmation or an unconfirmed reason.</summary>
    [Theory]
    [InlineData("""{"reason":"security","confirm":true,"jobVersion":"unexpected"}""")]
    [InlineData("""{"reason":"security","confirm":false}""")]
    [InlineData("""{"reason":"unknown","confirm":true}""")]
    [InlineData("""{"reason":"security","confirm":false,"confirm":true}""")]
    [InlineData("""{"reason":"security","confirm":true,"retired":true}""")]
    [InlineData("""{"reason":"security"}""")]
    public Task Given_UnsupportedRetirementBody_When_EitherBoundaryReads_Then_NoIntentIsAdmitted(string body) =>
        RequireAsync(http => SetBody(http, body), StatusCodes.Status400BadRequest);

    /// <summary>Missing and malformed preconditions remain distinct from canonical input that reaches native authentication.</summary>
    [Theory]
    [InlineData(null, 428)]
    [InlineData("*", 412)]
    [InlineData(Valid, 401)]
    public Task Given_RetirementVersion_When_EitherBoundaryReads_Then_ExactStrongPreconditionIsRequired(string? tag, int expected) =>
        RequireAsync(http => http.Request.Headers.IfMatch = tag == Valid ? ApplicationVersion.Create(Source, SourceRevision) : tag, expected);

    /// <summary>Repeated key headers and raw path aliases cannot hide behind canonical decoded route values.</summary>
    [Fact]
    public async Task Given_RepeatedKeyOrAliasPath_When_EitherBoundaryReads_Then_BadRequestIsFinite()
    {
        await RequireAsync(http => http.Request.Headers[KeyHeader] = new[] { RequestIdentifier, RequestIdentifier }, StatusCodes.Status400BadRequest);
        await RequireAsync(http => http.Features.Get<IHttpRequestFeature>()!.RawTarget = http.Request.Path.Value!.ToUpperInvariant(), StatusCodes.Status400BadRequest);
    }

    /// <summary>Separately scoped hashes bind original actor, application, version, key and finite reason without granting authority.</summary>
    [Fact]
    public void Given_OriginalIntent_When_ActorOrConfirmedInputChanges_Then_ReplayBindingsRemainDistinct()
    {
        var version = ApplicationVersion.Create(Source, SourceRevision);
        var intent = ApplicationRetirementIntent.Create(Source, version, RequestIdentifier, new(Reason, true));
        Assert.NotNull(intent);
        Assert.NotEqual(intent.ScopeHash(Source), intent.ScopeHash(RequestIdentifier));
        Assert.NotEqual(intent.RequestHash(), (intent with
        {
            Version = ApplicationVersion.Create(Source, 1)
        }).RequestHash());
        Assert.NotEqual(intent.RequestHash(), (intent with
        {
            Reason = ApplicationLifecycleReason.Maintenance
        }).RequestHash());
        Assert.Null(ApplicationRetirementIntent.Create(Source, Wildcard, RequestIdentifier, new(Reason, true)));
        Assert.Null(ApplicationRetirementIntent.Create("", version, RequestIdentifier, new(Reason, true)));
        Assert.Null(ApplicationRetirementIntent.Create(Source, version, RequestIdentifier, new(Reason, false)));
    }

    /// <summary>Returns one valid parsed request for independent transport tests without supplying real browser or API authority.</summary>
    internal static async Task<ApplicationMutationRequest> RequestAsync()
    {
        await using var services = Services();
        return await ApplicationMutationRequest.ReadAsync(Context(services), ApplicationProxyAction.Retire, Source, Now);
    }

    /// <summary>Runs each input through real API and BFF parsers using only finite output dependencies.</summary>
    private static async Task RequireAsync(Action<DefaultHttpContext> change, int expected)
    {
        await using var services = new ServiceCollection().AddLogging().AddProblemDetails().AddSingleton<TimeProvider>(TimeProvider.System)
            .AddSingleton(new BrowserSessionDestinations(Issuer, Origin + Separator)).BuildServiceProvider();
        var api = Context(services);
        api.Request.Path = ApiPath;
        api.Features.Get<IHttpRequestFeature>()!.RawTarget = ApiPath;
        api.Response.Body = new MemoryStream();
        change(api);
        await ApplicationRetirementEndpoint.HandleAsync(api, Source);
        Assert.Equal(expected, api.Response.StatusCode);
        Assert.Equal(NoStore, api.Response.Headers.CacheControl);
        Assert.Equal(0, api.Response.Headers.ETag.Count);
        var browser = Context(services);
        change(browser);
        if (expected == StatusCodes.Status401Unauthorized)
        {
            var request = await ApplicationMutationRequest.ReadAsync(browser, ApplicationProxyAction.Retire, Source, Now);
            Assert.Equal(ApiPath, request.Path);
            Assert.Equal(RequestIdentifier, request.RequestKey);
        }
        else
        {
            var rejected = await Assert.ThrowsAsync<ApplicationProxyRejected>(() => ApplicationMutationRequest.ReadAsync(browser, ApplicationProxyAction.Retire, Source, Now));
            Assert.Equal(expected, rejected.Status);
        }
    }

    /// <summary>Updates actual bytes and exact declared length together for JSON boundary probes.</summary>
    private static void SetBody(DefaultHttpContext http, string body)
    {
        var bytes = Encoding.UTF8.GetBytes(body);
        http.Request.ContentLength = bytes.Length;
        http.Request.Body = new MemoryStream(bytes);
    }

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
        http.Request.Headers[KeyHeader] = RequestIdentifier;
        http.Request.ContentType = JsonType;
        var bytes = Encoding.UTF8.GetBytes(body);
        http.Request.ContentLength = bytes.Length;
        http.Request.Body = new MemoryStream(bytes);
        return http;
    }
}
