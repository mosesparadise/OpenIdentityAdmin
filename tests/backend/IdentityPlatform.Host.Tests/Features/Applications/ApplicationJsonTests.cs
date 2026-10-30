using System.Text;
using IdentityPlatform.Admin.Bff.Features.Applications;
using IdentityPlatform.Admin.Bff.Features.BrowserSessions;
using IdentityPlatform.Core.Features.Applications;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Host.Tests.Features.Applications;

/// <summary>Exercises actual bounded JSON parsing and independent request/precondition failures before any remote dispatch.</summary>
public sealed class ApplicationJsonTests
{
    private const string Put = "PUT";
    private const string Separator = "/";
    private const string FaultValid = "valid";
    private const string FaultDuplicate = "duplicate";
    private const string EditTarget = "/api/v1/applications/application-id";
    private const string CreateTarget = "/api/v1/applications";
    private const string Https = "https";
    private const string Post = "POST";
    private const string JsonMedia = "application/json";
    private const string SyntheticHeader = "synthetic-native-validation-is-tested-separately";
    private const string FaultOrigin = "origin";
    private const string FaultFetch = "fetch";
    private const string FetchHeader = "Sec-Fetch-Site";
    private const string CrossSite = "cross-site";
    private const string FaultAuthorization = "authorization";
    private const string ForgedBearer = "Bearer attacker";
    private const string FaultDuplicateHeader = "duplicate-header";
    private const string FirstHeader = "one";
    private const string SecondHeader = "two";
    private const string FaultMissingHeader = "missing-header";
    private const string FaultMedia = "media";
    private const string TextMedia = "text/plain";
    private const string FaultMethod = "method";
    private const string Patch = "PATCH";
    private const string FaultQuery = "query";
    private const string ForeignQuery = "?target=attacker";
    private const string FaultRawAlias = "raw-alias";
    private const string AliasedTarget = "/bff/api/v1/%61pplications";
    private const string FaultDuplicateJson = "duplicate-json";
    private const string ClientValue = "\"example\"";
    private const string DuplicateClient = "\"example\",\"clientId\":\"second\"";
    private const string FaultUnknownJson = "unknown-json";
    private const string ObjectStart = "{";
    private const string UnknownSecret = "{\"secret\":\"attacker\",";
    private const string FaultNullJson = "null-json";
    private const string NullValue = "null";
    private const string FaultInvalidUnicode = "invalid-unicode";
    private const string InvalidUnicode = "\"\\uD800\"";
    private const string FaultDeclaredOverflow = "declared-overflow";
    private const string FaultStreamOverflow = "stream-overflow";
    private const string FaultLengthMismatch = "length-mismatch";
    private const string FaultTransfer = "transfer";
    private const string Chunked = "chunked";
    private const string FaultEncoding = "encoding";
    private const string Gzip = "gzip";

    private const string Origin = "https://bff.example.test";
    private const string Issuer = "https://issuer.example.test";
    private const string Path = "/bff/api/v1/applications";
    private const string Body = "{\"clientId\":\"example\",\"displayName\":\"Example\",\"redirectUris\":[\"https://app.example.test/callback\"],\"credentialExpiresAt\":\"2030-01-01T00:00:00Z\"}";
    private const string EditBody = "{\"displayName\":\"Edited\",\"redirectUris\":[\"https://app.example.test/callback\"]}";
    private const string Header = "X-CSRF-TOKEN";
    private const string Id = "application-id";
    private const int Limit = 32768;
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2029-01-01T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Strict framing and genuine JSON parsing reject independent attacker-controlled representations.</summary>
    [Theory]
    [InlineData("origin")]
    [InlineData("fetch")]
    [InlineData("authorization")]
    [InlineData("duplicate-header")]
    [InlineData("missing-header")]
    [InlineData("media")]
    [InlineData("method")]
    [InlineData("query")]
    [InlineData("raw-alias")]
    [InlineData("duplicate-json")]
    [InlineData("unknown-json")]
    [InlineData("null-json")]
    [InlineData("invalid-unicode")]
    [InlineData("declared-overflow")]
    [InlineData("stream-overflow")]
    [InlineData("length-mismatch")]
    [InlineData("transfer")]
    [InlineData("encoding")]
    public async Task Given_InvalidJsonMutation_When_Parsed_Then_InputFailsBeforeTransport(string fault)
    {
        using var services = Services();
        var http = Context(services);
        Change(http, fault);
        var error = await Assert.ThrowsAsync<ApplicationProxyRejected>(() => ApplicationMutationRequest.ReadAsync(http, ApplicationProxyAction.Create, null, Now));
        Assert.Equal(400, error.Status);
    }

    /// <summary>Canonical preconditions survive exactly; missing, duplicate and alternate strong-tag grammars retain precise failure statuses.</summary>
    [Theory]
    [InlineData(null, 428)]
    [InlineData("*", 412)]
    [InlineData("W/\"tag\"", 412)]
    [InlineData("duplicate", 412)]
    [InlineData("valid", 200)]
    public async Task Given_EditPrecondition_When_JsonParsed_Then_OnlyOneCanonicalVersionIsAccepted(string? input, int expected)
    {
        using var services = Services();
        var http = Context(services);
        http.Request.Method = Put;
        http.Request.Path = Path + Separator + Id;
        SetBody(http, EditBody);
        if (input is not null)
        {
            http.Request.Headers.IfMatch = input == FaultValid ? ApplicationVersion.Create(Id, 1) : input == FaultDuplicate ? new[] { ApplicationVersion.Create(Id, 1), ApplicationVersion.Create(Id, 1) } : input;
        }
        if (expected != StatusCodes.Status200OK)
        {
            Assert.Equal(expected, (await Assert.ThrowsAsync<ApplicationProxyRejected>(() => ApplicationMutationRequest.ReadAsync(http, ApplicationProxyAction.Edit, Id, Now))).Status);
            return;
        }
        var request = await ApplicationMutationRequest.ReadAsync(http, ApplicationProxyAction.Edit, Id, Now);
        Assert.Equal(HttpMethod.Put, request.Method);
        Assert.Equal(EditTarget, request.Path);
        Assert.Equal(ApplicationVersion.Create(Id, 1), request.Version);
        Assert.Equal(EditBody, Encoding.UTF8.GetString(request.Body.Span));
    }

    /// <summary>Freezes one finite create with an explicit expiry and no precondition; it accepts no arbitrary API routing input.</summary>
    [Fact]
    public async Task Given_ValidCreate_When_JsonParsed_Then_OnlyFixedNativeApiCommandIsProduced()
    {
        using var services = Services();
        var request = await ApplicationMutationRequest.ReadAsync(Context(services), ApplicationProxyAction.Create, null, Now);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal(CreateTarget, request.Path);
        Assert.Null(request.Version);
        Assert.Equal(Body, Encoding.UTF8.GetString(request.Body.Span));
    }

    /// <summary>A valid non-ASCII document at the exact byte limit remains byte-for-byte bounded without serializer escape expansion.</summary>
    [Fact]
    public async Task Given_ExactLimitUnicodeJson_When_CommandIsFrozen_Then_OriginalValidatedBytesArePreserved()
    {
        const string DisplayValue = "Example";
        const int DisplayCharacters = 100;
        using var services = Services();
        var http = Context(services);
        var text = Body.Replace(DisplayValue, new string('例', DisplayCharacters), StringComparison.Ordinal);
        text += new string(' ', Limit - Encoding.UTF8.GetByteCount(text));
        SetBody(http, text);
        var request = await ApplicationMutationRequest.ReadAsync(http, ApplicationProxyAction.Create, null, Now);
        Assert.Equal(Limit, request.Body.Length);
        Assert.Equal(Encoding.UTF8.GetBytes(text), request.Body.ToArray());
    }

    /// <summary>Serialization side effects cannot escape the final authority predicate even while the original request budget is still valid.</summary>
    [Fact]
    public async Task Given_AuthorityExpiresDuringSerialization_When_PublicationIsAttempted_Then_NoBytesAreWritten()
    {
        var current = true;
        var http = new DefaultHttpContext();
        http.Response.Body = new MemoryStream();
        using var budget = new ApplicationReadBudget(TimeProvider.System, TestContext.Current.CancellationToken);
        var value = new SerializationExpiry(() => current = false);
        var error = await Assert.ThrowsAsync<ApplicationProxyRejected>(() => ApplicationMutationBoundary.WriteAsync(http, value, StatusCodes.Status200OK, budget, () => current));
        Assert.Equal(401, error.Status);
        Assert.Equal(0, http.Response.Body.Length);
    }

    /// <summary>Advances only the authority observation during the actual serializer's property read.</summary>
    private sealed class SerializationExpiry(Action expire)
    {
        public bool Value
        {
            /// <summary>Changes the observed authority inside the real serialization read.</summary>
            get
            {
                expire();
                return true;
            }
        }
    }

    /// <summary>Constructs an ordinary JSON request with independent wire values, without installed sessions or a transport.</summary>
    private static DefaultHttpContext Context(IServiceProvider services)
    {
        var http = new DefaultHttpContext { RequestServices = services };
        http.RequestAborted = TestContext.Current.CancellationToken;
        http.Request.Scheme = Https;
        http.Request.Method = Post;
        http.Request.Path = Path;
        http.Request.ContentType = JsonMedia;
        http.Request.Headers.Origin = Origin;
        http.Request.Headers[Header] = SyntheticHeader;
        SetBody(http, Body);
        return http;
    }

    /// <summary>Changes one boundary independently, including actual streamed bytes rather than only advertised length.</summary>
    private static void Change(DefaultHttpContext http, string fault)
    {
        switch (fault)
        {
            case FaultOrigin:
                http.Request.Headers.Origin = Issuer;
                break;
            case FaultFetch:
                http.Request.Headers[FetchHeader] = CrossSite;
                break;
            case FaultAuthorization:
                http.Request.Headers.Authorization = ForgedBearer;
                break;
            case FaultDuplicateHeader:
                http.Request.Headers[Header] = new[] { FirstHeader, SecondHeader };
                break;
            case FaultMissingHeader:
                http.Request.Headers.Remove(Header);
                break;
            case FaultMedia:
                http.Request.ContentType = TextMedia;
                break;
            case FaultMethod:
                http.Request.Method = Patch;
                break;
            case FaultQuery:
                http.Request.QueryString = new(ForeignQuery);
                break;
            case FaultRawAlias:
                http.Features.Get<IHttpRequestFeature>()!.RawTarget = AliasedTarget;
                break;
            default:
                ChangeBody(http, fault);
                break;
        }
    }

    /// <summary>Changes actual body/media framing independently of provenance and route observations.</summary>
    private static void ChangeBody(DefaultHttpContext http, string fault)
    {
        switch (fault)
        {
            case FaultDuplicateJson:
                SetBody(http, Body.Replace(ClientValue, DuplicateClient, StringComparison.Ordinal));
                break;
            case FaultUnknownJson:
                SetBody(http, Body.Replace(ObjectStart, UnknownSecret, StringComparison.Ordinal));
                break;
            case FaultNullJson:
                SetBody(http, Body.Replace(ClientValue, NullValue, StringComparison.Ordinal));
                break;
            case FaultInvalidUnicode:
                SetBody(http, Body.Replace(ClientValue, InvalidUnicode, StringComparison.Ordinal));
                break;
            case FaultDeclaredOverflow:
                http.Request.ContentLength = Limit + 1;
                break;
            case FaultStreamOverflow:
                SetBody(http, Body + new string(' ', Limit + 1));
                http.Request.ContentLength = null;
                break;
            case FaultLengthMismatch:
                http.Request.ContentLength++;
                break;
            case FaultTransfer:
                http.Request.Headers.TransferEncoding = Chunked;
                break;
            case FaultEncoding:
                http.Request.Headers.ContentEncoding = Gzip;
                break;
        }
    }

    /// <summary>Uses only configured origins; missing session/transport registration proves these are pure admission controls.</summary>
    private static ServiceProvider Services() => new ServiceCollection().AddSingleton(new BrowserSessionDestinations(Issuer + Separator, Origin + Separator)).BuildServiceProvider();

    /// <summary>Replaces both actual bytes and ordinary declared length; selected tests then independently corrupt framing.</summary>
    private static void SetBody(HttpContext http, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        http.Request.Body = new MemoryStream(bytes);
        http.Request.ContentLength = bytes.Length;
    }
}
