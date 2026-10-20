using IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;
using Microsoft.AspNetCore.Http;

namespace IdentityPlatform.Host.Tests.Features.AdministratorClient;

/// <summary>Checks that a deliberate connection loss remains the same closed transport during a subsequent sanitized rejection attempt.</summary>
public sealed class NativeLostResponseTests
{
    /// <summary>The original publication and fallback both fail without a second loss observation or any forwarded response bytes.</summary>
    [Fact]
    public async Task Given_PostCommitResponseLoss_When_BoundaryAttemptsFallback_Then_TheSameLossRemainsWithoutRecounting()
    {
        var context = new DefaultHttpContext();
        context.Response.StatusCode = StatusCodes.Status201Created;
        await using var body = new MemoryStream();
        var inner = new StreamResponseBodyFeature(body);
        var observed = 0;
        var response = new NativeLostResponse(context, inner, () => observed++);
        var first = await Assert.ThrowsAsync<IOException>(() => response.StartAsync(TestContext.Current.CancellationToken));
        Assert.Equal(1, observed);
        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        var second = await Assert.ThrowsAsync<IOException>(() => response.StartAsync(TestContext.Current.CancellationToken));
        Assert.Same(first, second);
        Assert.Equal(1, observed);
        Assert.Equal(0, body.Length);
    }

    /// <summary>A loss fixture cannot turn an earlier unsuccessful response into credited post-commit loss evidence.</summary>
    [Fact]
    public async Task Given_UnsuccessfulInitialResponse_When_LossIsRequested_Then_NoLossIsCredited()
    {
        var context = new DefaultHttpContext();
        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        await using var body = new MemoryStream();
        var observed = 0;
        var response = new NativeLostResponse(context, new StreamResponseBodyFeature(body), () => observed++);
        var failure = await Record.ExceptionAsync(() => response.StartAsync(TestContext.Current.CancellationToken));
        Assert.NotNull(failure);
        Assert.False(response.Caused(failure));
        Assert.Equal(0, observed);
        Assert.Equal(0, body.Length);
        context.Response.StatusCode = StatusCodes.Status200OK;
        var loss = await Assert.ThrowsAsync<IOException>(() => response.StartAsync(TestContext.Current.CancellationToken));
        Assert.True(response.Caused(loss));
        Assert.False(response.Caused(new IOException()));
        Assert.False(response.Caused(new AggregateException(loss)));
        Assert.Equal(1, observed);
    }
}
