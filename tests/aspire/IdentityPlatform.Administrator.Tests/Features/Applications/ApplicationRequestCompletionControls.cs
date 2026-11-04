using Microsoft.AspNetCore.Http;

namespace IdentityPlatform.Administrator.Tests.Features.Applications;

/// <summary>Calibrates the two independent server-completion observations without pretending synthetic callbacks are hosted evidence.</summary>
internal static class ApplicationRequestCompletionControls
{
    private const string Path = "/bff/api/v1/applications";
    private const string OtherPath = "/unrelated";
    private const string Post = "POST";
    private const string Get = "GET";
    private const string Query = "?unexpected=true";
    private const int Created = 201;

    /// <summary>Requires both completion signals for exactly one attempt and rejects absent, duplicate, wrong-route and query-bearing evidence.</summary>
    internal static void Require()
    {
        using var valid = New();
        Assert.Null(valid.Enter(Http(OtherPath, Post)));
        Assert.Null(valid.Enter(Http(Path, Get)));
        var attempt = Assert.IsType<ApplicationRequestCompletion.Attempt>(valid.Enter(Http(Path, Post)));
        attempt.PipelineEnded(Created, true, false);
        Assert.False(valid.Completion.IsCompleted);
        attempt.ResponseEnded();
        Assert.True(valid.Completion.IsCompletedSuccessfully && valid.Requests == 1 && valid.Seal());
        Assert.False(valid.Seal());
        using var absent = New();
        var incomplete = Assert.IsType<ApplicationRequestCompletion.Attempt>(absent.Enter(Http(Path, Post)));
        incomplete.ResponseEnded();
        Assert.False(absent.Completion.IsCompleted || absent.Seal());
        RequireMissingResponse();
        using var duplicate = New();
        var first = Assert.IsType<ApplicationRequestCompletion.Attempt>(duplicate.Enter(Http(Path, Post)));
        _ = duplicate.Enter(Http(Path, Post));
        first.PipelineEnded(Created, true, false);
        first.ResponseEnded();
        Assert.False(duplicate.Seal());
        using var repeated = New();
        var repeat = Assert.IsType<ApplicationRequestCompletion.Attempt>(repeated.Enter(Http(Path, Post)));
        repeat.PipelineEnded(Created, true, false);
        repeat.ResponseEnded();
        repeat.ResponseEnded();
        Assert.False(repeated.Seal());
        using var query = New();
        var context = Http(Path, Post);
        context.Request.QueryString = new(Query);
        var bad = Assert.IsType<ApplicationRequestCompletion.Attempt>(query.Enter(context));
        bad.PipelineEnded(Created, true, false);
        bad.ResponseEnded();
        Assert.False(query.Seal());
    }

    /// <summary>Pipeline return without the server response callback cannot complete or seal an attempt.</summary>
    private static void RequireMissingResponse()
    {
        using var owner = New();
        var attempt = Assert.IsType<ApplicationRequestCompletion.Attempt>(owner.Enter(Http(Path, Post)));
        attempt.PipelineEnded(Created, true, false);
        Assert.False(owner.Completion.IsCompleted || owner.Seal());
    }

    /// <summary>Creates a detached synthetic observer; only real host callbacks can satisfy the separate browser journey.</summary>
    private static ApplicationRequestCompletion New() => new(Path, () => { });

    /// <summary>Provides only fixed route metadata and never simulates a network request or response publication.</summary>
    private static DefaultHttpContext Http(string path, string method)
    {
        var value = new DefaultHttpContext();
        value.Request.Path = path;
        value.Request.Method = method;
        return value;
    }
}
