using IdentityPlatform.Host.Tests.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolTransport;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace IdentityPlatform.Host.Tests.Features.ProtocolTransport;

/// <summary>Exercises failure paths that must discard all staged cookies, headers and bytes.</summary>
public sealed class ResponseFailureTests
{
    private const int OversizeBody = 65537;
    private const int OversizeValues = 4097;
    private const int OversizeHeaderBytes = 16385;
    private const int OversizeCallbacks = 17;

    private const string CompletionCase = "completion";
    private const string StreamingCase = "streaming";
    private const string FileCase = "file";
    private const string BodyLimitCase = "body-limit";
    private const string HeaderLimitCase = "header-limit";
    private const string CallbackLimitCase = "callback-limit";
    private const string CallbackFailureCase = "callback-failure";
    private const string ReplacementCase = "body-replacement";
    private const string EmptyValuesCase = "empty-values";
    private const string PipeLimitCase = "pipe-limit";
    private const string CookieName = "synthetic";
    private const string CookieValue = "must-not-escape";
    private const string PreviousCookieName = "old-synthetic";
    private const string UnusedFile = "unused";
    private const string EmptyHeader = "X-Empty";
    private const string OversizeHeader = "X-Oversize";
    private const string CallbackFailure = "synthetic callback failure";
    private const string ProducerFailure = "synthetic producer failure";

    /// <summary>Latches swallowed unsupported features and bounded-buffer overflows before any success response can escape.</summary>
    [Theory]
    [InlineData(CompletionCase)]
    [InlineData(StreamingCase)]
    [InlineData(FileCase)]
    [InlineData(BodyLimitCase)]
    [InlineData(HeaderLimitCase)]
    [InlineData(EmptyValuesCase)]
    [InlineData(PipeLimitCase)]
    [InlineData(CallbackLimitCase)]
    [InlineData(CallbackFailureCase)]
    [InlineData(ReplacementCase)]
    public async Task Given_StagedResponse_When_UnsupportedOrOversizeOperationIsSwallowed_Then_PublicationFails(string failure)
    {
        await using var probe = new StorageRequestProbe();
        using var output = new MemoryStream();
        var http = new DefaultHttpContext { RequestServices = probe.Root };
        http.Response.Body = output;
        _ = http.Response.Cookies;
        var original = http.Features.GetRequiredFeature<IHttpResponseFeature>();
        await ProtocolHttpBoundary.RunAsync(http, probe.Factory, probe.Clock, async (_, _, _) =>
        {
            http.Response.Cookies.Append(CookieName, CookieValue);
            http.Response.Cookies.Delete(PreviousCookieName);
            Assert.Equal(0, original.Headers.SetCookie.Count);
            try
            {
                await FailAsync(http, failure);
            }
            catch (InvalidOperationException) { }
            return ProtocolResponseOutcome.ReadOnly;
        });
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, http.Response.StatusCode);
        Assert.Empty(output.ToArray());
        Assert.Equal(0, original.Headers.SetCookie.Count);
        Assert.Equal(1, Assert.Single(probe.Databases).Disposals);
    }

    /// <summary>Exercises unsupported framework APIs without substituting a fake response capture.</summary>
    private static async Task FailAsync(HttpContext http, string failure)
    {
        var body = http.Features.GetRequiredFeature<IHttpResponseBodyFeature>();
        switch (failure)
        {
            case CompletionCase:
                http.Response.OnCompleted(() => Task.CompletedTask);
                break;
            case StreamingCase:
                body.DisableBuffering();
                break;
            case FileCase:
                await body.SendFileAsync(UnusedFile, 0, null, TestContext.Current.CancellationToken);
                break;
            case BodyLimitCase:
                await body.Stream.WriteAsync(new byte[OversizeBody], TestContext.Current.CancellationToken);
                break;
            case EmptyValuesCase:
                http.Response.Headers[EmptyHeader] = new string[OversizeValues];
                break;
            case PipeLimitCase:
                body.Writer.GetMemory(OversizeBody);
                break;
            case HeaderLimitCase:
                http.Response.Headers[OversizeHeader] = new string('x', OversizeHeaderBytes);
                break;
            case CallbackLimitCase:
                for (var index = 0; index < OversizeCallbacks; index++)
                {
                    http.Response.OnStarting(() => Task.CompletedTask);
                }
                break;
            case CallbackFailureCase:
                http.Response.OnStarting(() => throw new InvalidOperationException(CallbackFailure));
                break;
            case ReplacementCase:
                http.Response.Body = Stream.Null;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(failure));
        }
    }

    /// <summary>Retains callbacks and their services when a producer abandons a write then throws.</summary>
    [Fact]
    public async Task Given_AbandonedWrite_When_CallbackIsBlocked_Then_BoundaryRetainsContextUntilActualCompletion()
    {
        await using var probe = new StorageRequestProbe();
        using var output = new MemoryStream();
        var http = new DefaultHttpContext { RequestServices = probe.Root };
        http.Response.Body = output;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task? write = null;
        var boundary = ProtocolHttpBoundary.RunAsync(http, probe.Factory, probe.Clock, (_, _, _) =>
        {
            http.Response.OnStarting(async () => { entered.SetResult(); await release.Task; });
            write = http.Response.Body.WriteAsync(new byte[] { 1 }, TestContext.Current.CancellationToken).AsTask();
            throw new InvalidOperationException(ProducerFailure);
        });
        try
        {
            await entered.Task.WaitAsync(TestContext.Current.CancellationToken);
            Assert.False(boundary.IsCompleted);
            Assert.Equal(0, Assert.Single(probe.Databases).Disposals);
            Assert.NotSame(probe.Root, http.RequestServices);
        }
        finally { release.TrySetResult(); }
        await boundary;
        await Assert.ThrowsAsync<InvalidOperationException>(() => write!);
        Assert.Equal(1, Assert.Single(probe.Databases).Disposals);
        Assert.Same(probe.Root, http.RequestServices);
        Assert.Empty(output.ToArray());
    }
}
