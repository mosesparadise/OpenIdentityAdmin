using IdentityPlatform.Host.Tests.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolTransport;
using Microsoft.AspNetCore.Http;

namespace IdentityPlatform.Host.Tests.Features.ProtocolTransport;

/// <summary>Proves caught independent cancellation and concurrent producer overflow cannot publish truncated success.</summary>
public sealed class ResponseCancellationTests
{
    private const int MaximumBodyBytes = 65536;
    private const int OverHalfCapacity = 32769;

    private const string StagedPrefix = "prefix";

    /// <summary>Rejects the whole response when a second independently cancelled write is caught by the producer.</summary>
    [Fact]
    public async Task Given_StagedPrefix_When_CancelledWriteIsSwallowed_Then_NoTruncatedSuccessEscapes()
    {
        await using var probe = new StorageRequestProbe();
        using var output = new MemoryStream();
        using var cancelled = new CancellationTokenSource();
        var http = new DefaultHttpContext { RequestServices = probe.Root };
        http.Response.Body = output;
        await ProtocolHttpBoundary.RunAsync(http, probe.Factory, probe.Clock, async (_, _, _) =>
        {
            await http.Response.WriteAsync(StagedPrefix, TestContext.Current.CancellationToken);
            await cancelled.CancelAsync();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => http.Response.Body.WriteAsync(new byte[] { 1 }, cancelled.Token).AsTask());
            return ProtocolResponseOutcome.ReadOnly;
        });
        Assert.Empty(output.ToArray());
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, http.Response.StatusCode);
    }

    /// <summary>Two simultaneously released near-capacity writes cannot both pass the original remaining-byte check.</summary>
    [Fact]
    public async Task Given_ConcurrentProducers_When_CombinedBodyExceedsBound_Then_ResponseCannotBeAdmitted()
    {
        await using var probe = new StorageRequestProbe();
        var request = new ProtocolStorageRequest(probe.Clock, TestContext.Current.CancellationToken);
        using var capture = new ProtocolResponseCapture(request);
        await capture.StartAsync(TestContext.Current.CancellationToken);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = WriteAsync(capture, release.Task);
        var second = WriteAsync(capture, release.Task);
        release.SetResult();
        var failures = await Task.WhenAll(first, second);
        Assert.Contains(true, failures);
        Assert.InRange(capture.Bytes.Length, 0, MaximumBodyBytes);
        Assert.Throws<InvalidOperationException>(() => request.RequirePublication(ProtocolResponseOutcome.ReadOnly));
    }

    /// <summary>Uses an independent execution context to race two supported synchronous producer calls.</summary>
    private static Task<bool> WriteAsync(ProtocolResponseCapture capture, Task release) => Task.Run(async () =>
    {
        await release;
        try
        {
            capture.Stream.Write(new byte[OverHalfCapacity]);
            return false;
        }
        catch (InvalidOperationException) { return true; }
    }, TestContext.Current.CancellationToken);
}
