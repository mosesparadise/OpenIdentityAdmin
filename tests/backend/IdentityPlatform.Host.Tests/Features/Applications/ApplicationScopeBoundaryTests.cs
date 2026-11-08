using System.Text;
using System.Text.Json;
using IdentityPlatform.Admin.Api.Features.Applications;
using IdentityPlatform.Core.Features.Applications;
using IdentityPlatform.Infrastructure.Features.Applications;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Host.Tests.Features.Applications;

/// <summary>Exercises assignment input and profile boundaries without replacing native provider or authenticated HTTP acceptance.</summary>
public sealed class ApplicationScopeBoundaryTests
{
    private const string Id = "scope-client";
    private const string Path = "/api/v1/applications/scope-client/scopes";
    private const string Body = "{\"scopes\":[\"email\"]}";
    private const string JsonType = "application/json";
    private const string NoStore = "no-store";
    private const string NoSniff = "nosniff";
    private const string Status = "status";
    private const string Email = "email";
    private const string Orders = "orders.read";
    private const string Profile = "profile";
    private const string BadTag = "*";
    private const string Extra = "?extra=true";
    private const int MaximumBytes = 32768;

    private static readonly string[] InvalidBodies = { "{}", "{\"scopes\":null}", "{\"scopes\":[\"openid\"]}", "{\"scopes\":[\"email\",\"email\"]}",
            "{\"scopes\":[],\"scopes\":[\"email\"]}", "{\"scopes\":[],\"permissions\":[]}", "{\"scopes\":[null]}" };

    private const int TooManyScopes = 33;

    /// <summary>Assignment intent copies caller collections and rejects reserved, duplicate, overlong and malformed target/precondition values.</summary>
    [Fact]
    public void Given_CallerOwnedAssignment_When_Frozen_Then_MutationCannotAlterIntentAndInvalidSetsReject()
    {
        string[] names = [Orders, Email];
        var command = ApplicationMutationCommand.AssignScopes(Id, ApplicationVersion.Create(Id, 1), new(names));
        Assert.NotNull(command);
        names[0] = Profile;
        Assert.Equal([Email, Orders], command.Scopes);
        Assert.NotNull(ApplicationMutationCommand.AssignScopes(Id, ApplicationVersion.Create(Id, 1), new([])));
        foreach (var invalid in new[] { new[] { Email, Email }, new[] { Profile }, new[] { Email + ' ' }, new string[TooManyScopes], null })
        {
            Assert.Null(ApplicationMutationCommand.AssignScopes(Id, ApplicationVersion.Create(Id, 1), new(invalid!)));
        }
        Assert.Null(ApplicationMutationCommand.AssignScopes(Id, BadTag, new([Email])));
        Assert.Null(ApplicationMutationCommand.AssignScopes(string.Empty, ApplicationVersion.Create(Id, 1), new([Email])));
    }

    /// <summary>Missing tags remain428 while malformed tags and strict JSON errors are400; canonical input reaches genuine bearer admission only.</summary>
    [Fact]
    public async Task Given_AssignmentInput_When_PreconditionOrJsonIsInvalid_Then_NoNativeDependencyIsReached()
    {
        await RequireAsync(http => http.Request.Headers.IfMatch = default, StatusCodes.Status428PreconditionRequired);
        await RequireAsync(http => http.Request.Headers.IfMatch = BadTag, StatusCodes.Status400BadRequest);
        foreach (var body in InvalidBodies)
        {
            await RequireAsync(http => SetBody(http, body), StatusCodes.Status400BadRequest);
        }
        await RequireAsync(_ => { }, StatusCodes.Status401Unauthorized);
        await RequireAsync(http => SetBody(http, Body.PadRight(MaximumBytes)), StatusCodes.Status401Unauthorized);
        await RequireAsync(http => SetBody(http, Body.PadRight(MaximumBytes + 1)), StatusCodes.Status400BadRequest);
    }

    /// <summary>Exact method/path/framing precedes current authority; even safe-looking aliases cannot select a different mutation route.</summary>
    [Fact]
    public async Task Given_AssignmentFraming_When_AliasedOrContradictory_Then_BadRequestHasNoSuccessMetadata()
    {
        await RequireAsync(http => http.Request.Scheme = Uri.UriSchemeHttp, StatusCodes.Status400BadRequest);
        await RequireAsync(http => http.Request.Method = HttpMethods.Post, StatusCodes.Status400BadRequest);
        await RequireAsync(http => http.Request.QueryString = new(Extra), StatusCodes.Status400BadRequest);
        await RequireAsync(http => http.Features.Get<IHttpRequestFeature>()!.RawTarget = Path.ToUpperInvariant(), StatusCodes.Status400BadRequest);
        await RequireAsync(http => http.Request.ContentLength = 1, StatusCodes.Status400BadRequest);
        await RequireAsync(http => http.Request.ContentType = null, StatusCodes.Status400BadRequest);
    }

    /// <summary>Runs the production boundary with real framing and captures only finite public problem metadata.</summary>
    private static async Task RequireAsync(Action<DefaultHttpContext> change, int expected)
    {
        await using var services = new ServiceCollection().AddLogging().AddProblemDetails().AddSingleton(TimeProvider.System).BuildServiceProvider();
        var http = new DefaultHttpContext { RequestServices = services };
        http.Request.Scheme = Uri.UriSchemeHttps;
        http.Request.Method = HttpMethods.Put;
        http.Request.Path = Path;
        http.Features.Get<IHttpRequestFeature>()!.RawTarget = Path;
        http.Request.ContentType = JsonType;
        http.Request.Headers.IfMatch = ApplicationVersion.Create(Id, 1);
        SetBody(http, Body);
        await using var output = new MemoryStream();
        http.Response.Body = output;
        change(http);
        await ApplicationScopesEndpoint.WriteAsync(http, Id);
        Assert.Equal(expected, http.Response.StatusCode);
        Assert.Equal(NoStore, http.Response.Headers.CacheControl);
        Assert.Equal(NoSniff, http.Response.Headers.XContentTypeOptions);
        Assert.Equal(0, http.Response.Headers.ETag.Count);
        using var json = JsonDocument.Parse(output.ToArray());
        Assert.Equal(expected, json.RootElement.GetProperty(Status).GetInt32());
    }

    /// <summary>Supplies actual UTF8 bytes independently of contradictory declared lengths.</summary>
    private static void SetBody(DefaultHttpContext http, string body) => http.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
}
