using System.Text;
using System.Text.Json;
using IdentityPlatform.Admin.Api.Features.Applications;
using IdentityPlatform.Core.Features.Applications;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Host.Tests.Features.Applications;

/// <summary>Exercises the actual API update boundary before native dependencies, distinguishing framing, precondition and immutable-input failures.</summary>
public sealed class ApplicationUpdateEndpointTests
{
    private const string Id = "managed-update";
    private const string Path = "/api/v1/applications/" + Id;
    private const string Replacement = "/secret-replacements";
    private const string Edit = "{\"displayName\":\"Updated\",\"redirectUris\":[\"https://client.example.test/callback\"]}";
    private const string Replace = "{\"confirmed\":true,\"credentialExpiresAt\":\"2099-01-01T00:00:00Z\"}";
    private const string JsonType = "application/json";
    private const string Status = "status";
    private const string Title = "title";
    private const string Precondition = "Reload the application before submitting a new change.";
    private const string NoStore = "no-store";
    private const string NoSniff = "nosniff";
    private const string Transfer = "Transfer-Encoding";
    private const string Chunked = "chunked";
    private const string EncodingHeader = "Content-Encoding";
    private const string Gzip = "gzip";
    private const string Query = "?extra=1";
    private const string Wildcard = "*";
    private const string Weak = "W/";
    private const string Invalid = "invalid";
    private const string Comma = ",";
    private const string Plain = "text/plain";
    private const int Limit = 32768;

    /// <summary>Missing and noncanonical tags remain distinct from a valid precondition that reaches native bearer admission.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_UpdatePreconditions_When_MissingMalformedOrCanonical_Then_FiniteStatusesPrecedeNativeStorage(bool replacement)
    {
        await RequireAsync(replacement, http => http.Request.Headers.IfMatch = default, StatusCodes.Status428PreconditionRequired);
        foreach (var tag in new[] { Wildcard, Weak + ApplicationVersion.Create(Id, 1), Invalid, ApplicationVersion.Create(Id, 1) + Comma + ApplicationVersion.Create(Id, 1) })
        {
            await RequireAsync(replacement, http => http.Request.Headers.IfMatch = tag, StatusCodes.Status412PreconditionFailed);
        }
        await RequireAsync(replacement, http => http.Request.Headers.IfMatch = new[] { ApplicationVersion.Create(Id, 1), ApplicationVersion.Create(Id, 1) }, StatusCodes.Status412PreconditionFailed);
        await RequireAsync(replacement, _ => { }, StatusCodes.Status401Unauthorized);
    }

    /// <summary>Exact HTTPS path/method/body framing cannot be bypassed by routing aliases or contradictory byte lengths.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_UpdateFraming_When_PathMethodOrBodyIsAliased_Then_BadRequestPrecedesNativeStorage(bool replacement)
    {
        await RequireAsync(replacement, http => http.Request.Scheme = Uri.UriSchemeHttp, StatusCodes.Status400BadRequest);
        await RequireAsync(replacement, http => http.Request.Method = HttpMethods.Get, StatusCodes.Status400BadRequest);
        await RequireAsync(replacement, http => http.Request.QueryString = new QueryString(Query), StatusCodes.Status400BadRequest);
        await RequireAsync(replacement, http => http.Features.Get<IHttpRequestFeature>()!.RawTarget = http.Request.Path.Value!.ToUpperInvariant(), StatusCodes.Status400BadRequest);
        await RequireAsync(replacement, http => http.Request.ContentType = Plain, StatusCodes.Status400BadRequest);
        await RequireAsync(replacement, http => http.Request.Headers[Transfer] = Chunked, StatusCodes.Status400BadRequest);
        await RequireAsync(replacement, http => http.Request.Headers[EncodingHeader] = Gzip, StatusCodes.Status400BadRequest);
        await RequireAsync(replacement, http => http.Request.ContentLength = 1, StatusCodes.Status400BadRequest);
        await RequireAsync(replacement, http => Body(http, (replacement ? Replace : Edit).PadRight(Limit)), StatusCodes.Status401Unauthorized);
        await RequireAsync(replacement, http => Body(http, (replacement ? Replace : Edit).PadRight(Limit + 1)), StatusCodes.Status400BadRequest);
    }

    /// <summary>PUT never accepts lifecycle/identity fields and replacement never accepts missing confirmation, alias dates or unknown properties.</summary>
    [Theory]
    [InlineData(false, "{\"displayName\":\"Updated\",\"redirectUris\":[\"https://client.example.test/callback\"],\"clientId\":\"changed\"}")]
    [InlineData(false, "{\"displayName\":\"Updated\",\"redirectUris\":[\"https://client.example.test/callback\"],\"credentialExpiresAt\":\"2099-01-01T00:00:00Z\"}")]
    [InlineData(false, "{\"displayName\":\"Updated\",\"displayName\":\"duplicate\",\"redirectUris\":[\"https://client.example.test/callback\"]}")]
    [InlineData(true, "{\"confirmed\":false,\"credentialExpiresAt\":\"2099-01-01T00:00:00Z\"}")]
    [InlineData(true, "{\"confirmed\":true,\"credentialExpiresAt\":\"2099-01-01T00:00:00+00:00\"}")]
    [InlineData(true, "{\"confirmed\":true,\"credentialExpiresAt\":\"2099-01-01T00:00:00Z\",\"clientSecret\":\"never-accepted\"}")]
    public Task Given_ImmutableOrInvalidUpdateInput_When_Parsed_Then_BadRequestPrecedesNativeStorage(bool replacement, string body) =>
        RequireAsync(replacement, http => Body(http, body), StatusCodes.Status400BadRequest);

    /// <summary>Creates only finite output dependencies; accidental native resolution produces 503 rather than satisfying the expected admission result.</summary>
    private static async Task RequireAsync(bool replacement, Action<DefaultHttpContext> change, int expected)
    {
        await using var services = new ServiceCollection().AddLogging().AddProblemDetails().AddSingleton(TimeProvider.System).BuildServiceProvider();
        var http = new DefaultHttpContext { RequestServices = services };
        http.Request.Scheme = Uri.UriSchemeHttps;
        http.Request.Method = replacement ? HttpMethods.Post : HttpMethods.Put;
        http.Request.Path = Path + (replacement ? Replacement : string.Empty);
        http.Features.Get<IHttpRequestFeature>()!.RawTarget = http.Request.Path;
        http.Request.ContentType = JsonType;
        http.Request.Headers.IfMatch = ApplicationVersion.Create(Id, 1);
        Body(http, replacement ? Replace : Edit);
        await using var output = new MemoryStream();
        http.Response.Body = output;
        change(http);
        await ApplicationUpdateEndpoint.HandleAsync(http, Id, replacement);
        Assert.Equal(expected, http.Response.StatusCode);
        Assert.Equal(NoStore, http.Response.Headers.CacheControl);
        Assert.Equal(NoSniff, http.Response.Headers.XContentTypeOptions);
        Assert.Equal(0, http.Response.Headers.ETag.Count);
        using var json = JsonDocument.Parse(output.ToArray());
        Assert.Equal(expected, json.RootElement.GetProperty(Status).GetInt32());
        if (expected is StatusCodes.Status412PreconditionFailed or StatusCodes.Status428PreconditionRequired)
        {
            Assert.Equal(Precondition, json.RootElement.GetProperty(Title).GetString());
        }
    }

    /// <summary>Supplies actual UTF-8 bytes independently of any declared length.</summary>
    private static void Body(DefaultHttpContext http, string body) => http.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
}
