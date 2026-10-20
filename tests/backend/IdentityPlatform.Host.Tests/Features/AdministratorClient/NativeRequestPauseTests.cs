using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace IdentityPlatform.Host.Tests.Features.AdministratorClient;

/// <summary>Requires original server cancellation independently of cleanup and rejects duplicate interception or unrelated requests.</summary>
public sealed class NativeRequestPauseTests
{
    private const string Path = "/synthetic-token";
    private const string OtherPath = "/synthetic-other";

    /// <summary>Cleanup terminates paused work but cannot supply the cancellation evidence required by the provider scenario.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Given_InterceptedRequest_When_CancelledOrReleased_Then_OnlyOriginalTokenCancellationBeforeCleanupIsCredited(bool cancel)
    {
        using var source = new CancellationTokenSource();
        var lifetime = new NativeRequestLifetime(source.Token);
        var context = Context(lifetime);
        var pause = new NativeRequestPause(Path);
        var operation = pause.TryPauseAsync(context);
        try
        {
            await pause.Reached.WaitAsync(TestContext.Current.CancellationToken);
            Assert.False(operation.IsCompleted);
            Assert.Throws<InvalidOperationException>(pause.RequireCancelled);
            if (cancel)
            {
                pause.BeginCancellation();
                await source.CancelAsync();
                await pause.Cancelled.WaitAsync(TestContext.Current.CancellationToken);
                pause.RequireCancelled();
            }
        }
        finally
        {
            pause.Release();
            await operation;
        }
        Assert.True(await operation);
        Assert.True(lifetime.Aborted);
        Assert.Equal(cancel, pause.Cancelled.IsCompleted);
        Assert.Throws<InvalidOperationException>(pause.RequireCancelled);
    }

    /// <summary>Only one exact mutation endpoint is intercepted, and a second matching send cannot silently reuse the pause.</summary>
    [Fact]
    public async Task Given_ExactOneShotPause_When_UnrelatedAndRepeatedRequestsArrive_Then_OnlyFirstMatchingPostCanWait()
    {
        using var source = new CancellationTokenSource();
        var lifetime = new NativeRequestLifetime(source.Token);
        var context = Context(lifetime);
        var pause = new NativeRequestPause(Path);
        context.Request.Path = OtherPath;
        Assert.False(await pause.TryPauseAsync(context));
        context.Request.Path = Path;
        context.Request.Method = HttpMethods.Post.ToLowerInvariant();
        Assert.False(await pause.TryPauseAsync(context));
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = Path.ToUpperInvariant();
        Assert.False(await pause.TryPauseAsync(context));
        context.Request.Path = Path;
        context.Request.Method = HttpMethods.Get;
        Assert.False(await pause.TryPauseAsync(context));
        context.Request.Method = HttpMethods.Post;
        var original = pause.TryPauseAsync(context);
        try
        {
            await pause.Reached.WaitAsync(TestContext.Current.CancellationToken);
            await Assert.ThrowsAsync<InvalidOperationException>(() => pause.TryPauseAsync(context));
        }
        finally { pause.Release(); await original; }
        Assert.False(pause.Cancelled.IsCompleted);
        Assert.True(lifetime.Aborted);
    }

    /// <summary>A disconnect that preceded the explicit client action cannot be retroactively relabelled as successful cancellation evidence.</summary>
    [Fact]
    public async Task Given_ServerCancellationBeforeExplicitAction_When_TestTriesToClaimIt_Then_EvidenceIsRejected()
    {
        using var source = new CancellationTokenSource();
        var pause = new NativeRequestPause(Path);
        var operation = pause.TryPauseAsync(Context(new NativeRequestLifetime(source.Token)));
        try
        {
            await pause.Reached.WaitAsync(TestContext.Current.CancellationToken);
            await source.CancelAsync();
            await pause.Cancelled.WaitAsync(TestContext.Current.CancellationToken);
            Assert.Throws<InvalidOperationException>(pause.BeginCancellation);
            Assert.Throws<InvalidOperationException>(pause.RequireCancelled);
        }
        finally { pause.Release(); await operation; }
    }

    /// <summary>Installs an independently cancellable lifetime so an abort observation cannot itself cancel the original token.</summary>
    private static DefaultHttpContext Context(NativeRequestLifetime lifetime)
    {
        var context = new DefaultHttpContext();
        context.Features.Set<IHttpRequestLifetimeFeature>(lifetime);
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = Path;
        return context;
    }
}
