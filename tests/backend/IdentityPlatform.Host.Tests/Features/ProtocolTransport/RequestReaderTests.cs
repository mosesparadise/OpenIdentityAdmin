using System.IO.Pipelines;
using System.Text;
using IdentityPlatform.Host.Tests.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolTransport;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace IdentityPlatform.Host.Tests.Features.ProtocolTransport;

/// <summary>Preserves host-owned framework input cleanup while rejecting arbitrary callbacks and replacement readers inside captured responses.</summary>
public sealed class RequestReaderTests
{
    private const string Form = "field=value";
    private const string Field = "field";
    private const string Value = "value";
    private const string MediaType = "application/x-www-form-urlencoded";

    /// <summary>Native form extraction can initialize its reader, but child completion callbacks still poison publication.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_FrameworkFormReader_When_ResponseIsCaptured_Then_OnlyOriginalHostOwnsInputCompletion(bool forbiddenCallback)
    {
        await using var storage = new StorageRequestProbe(clock: new ControlledProbeClock());
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(Form));
        using var output = new MemoryStream();
        var host = new HostResponse();
        var http = Create(storage, input, output, host);
        var parsed = false;
        var rejected = false;
        await ProtocolHttpBoundary.RunAsync(http, storage.Factory, storage.Clock, async (_, _, token) =>
        {
            var values = await http.Request.ReadFormAsync(token);
            parsed = values[Field] == Value;
            if (forbiddenCallback)
            {
                Assert.Throws<InvalidOperationException>(() => http.Response.OnCompleted(() => Task.CompletedTask));
                rejected = true;
            }
            return ProtocolResponseOutcome.ReadOnly;
        });
        Assert.True(parsed);
        Assert.Equal(forbiddenCallback, rejected);
        Assert.Equal(forbiddenCallback ? StatusCodes.Status503ServiceUnavailable : StatusCodes.Status200OK, http.Response.StatusCode);
        var completion = Assert.Single(host.Completions);
        Assert.IsAssignableFrom<PipeReader>(completion.State);
        Assert.Same(http.Request.BodyReader, completion.State);
        Assert.Same(storage.Root, http.RequestServices);
        await completion.Callback(completion.State);
    }

    /// <summary>A replacement input stream cannot register new host cleanup while owned capture is active.</summary>
    [Fact]
    public async Task Given_InitializedReader_When_BodyIsReplacedInsideCapture_Then_NewCompletionRegistrationIsRejected()
    {
        await using var storage = new StorageRequestProbe(clock: new ControlledProbeClock());
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(Form));
        using var replacement = new MemoryStream(Encoding.UTF8.GetBytes(Form));
        using var output = new MemoryStream();
        var host = new HostResponse();
        var http = Create(storage, input, output, host);
        var rejected = false;
        await ProtocolHttpBoundary.RunAsync(http, storage.Factory, storage.Clock, (_, _, _) =>
        {
            http.Request.Body = replacement;
            Assert.Throws<InvalidOperationException>(() => http.Request.BodyReader);
            rejected = true;
            return ValueTask.FromResult(ProtocolResponseOutcome.ReadOnly);
        });
        Assert.True(rejected);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, http.Response.StatusCode);
        Assert.Empty(output.ToArray());
        var completion = Assert.Single(host.Completions);
        await completion.Callback(completion.State);
    }

    /// <summary>Installs ordinary framework request features and a separately observable host completion owner.</summary>
    private static DefaultHttpContext Create(StorageRequestProbe storage, Stream input, Stream output, HostResponse host)
    {
        var http = new DefaultHttpContext { RequestServices = storage.Root };
        http.Features.Set<IHttpResponseFeature>(host);
        http.Response.Body = output;
        http.Request.Method = HttpMethods.Post;
        http.Request.ContentType = MediaType;
        http.Request.ContentLength = input.Length;
        http.Request.Body = input;
        return http;
    }

    /// <summary>Records the real framework completion callback so the synthetic host can execute it after the owned boundary ends.</summary>
    private sealed class HostResponse : HttpResponseFeature
    {
        internal List<(Func<object, Task> Callback, object State)> Completions { get; } = [];
        /// <summary>Retains original host callbacks without forwarding them into any owned child scope.</summary>
        public override void OnCompleted(Func<object, Task> callback, object state) => Completions.Add((callback, state));
    }
}
