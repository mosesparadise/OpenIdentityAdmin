using System.Text;
using System.Text.Json;
using IdentityPlatform.AuthServer.Features.Protocol.UserInfo;
using IdentityPlatform.Infrastructure.Features.Identity.ManagedApplications;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolTransport;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Host.Tests.Features.ManagedApplications;

/// <summary>Exercises the real Minimal API JSON formatter against the owned bounded response writer, including its native pipe accounting contract.</summary>
public sealed class UserInfoResponseTests
{
    private const string Subject = "opaque-subject";
    private const string Address = "synthetic@example.test";
    private const string SubjectField = "sub";
    private const string EmailField = "email";
    private const string VerifiedField = "email_verified";
    private const string InjectedHeader = "X-Unapproved-Profile";
    private const string InjectedValue = "synthetic";

    /// <summary>Real asynchronous JSON output stays exact and finite through flush, callbacks and sealing; no email is fabricated.</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Given_ConsentedProjection_When_MinimalApiFormatsIntoOwnedPipe_Then_ExactProfileSurvivesActualSeal(bool disclose, bool verified)
    {
        await using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        var request = new ProtocolStorageRequest(TimeProvider.System, TestContext.Current.CancellationToken);
        using var capture = new ProtocolResponseCapture(request);
        var http = new DefaultHttpContext { RequestServices = services };
        http.Features.Set<IHttpResponseFeature>(capture);
        http.Features.Set<IHttpResponseBodyFeature>(capture);
        var callbacks = 0;
        http.Response.OnStarting(() => { callbacks++; return Task.CompletedTask; });
        var response = new UserInfoResponse(http, Subject, disclose ? new ManagedEmailProjection(Address, verified) : null);
        await response.FormatAsync(http);
        await capture.SealAsync();
        response.RequireCurrent(http);
        Assert.Equal(1, callbacks);
        using var body = JsonDocument.Parse(capture.Bytes);
        Assert.Equal(Subject, body.RootElement.GetProperty(SubjectField).GetString());
        Assert.True(body.RootElement.EnumerateObject().Select(field => field.Name).ToHashSet(StringComparer.Ordinal)
            .SetEquals(disclose ? [SubjectField, EmailField, VerifiedField] : [SubjectField]));
        if (disclose)
        {
            Assert.Equal(Address, body.RootElement.GetProperty(EmailField).GetString());
            Assert.Equal(verified ? JsonValueKind.True : JsonValueKind.False, body.RootElement.GetProperty(VerifiedField).ValueKind);
        }
    }

    /// <summary>The actual asynchronous JSON flush runs callbacks before returning, so added headers must already fail the immediate output check.</summary>
    [Fact]
    public async Task Given_ResponseCallbackAddsHeader_When_RealJsonFlushExecutes_Then_ProjectionRejectsChangedOutput()
    {
        await using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        var request = new ProtocolStorageRequest(TimeProvider.System, TestContext.Current.CancellationToken);
        using var capture = new ProtocolResponseCapture(request);
        var http = new DefaultHttpContext { RequestServices = services };
        http.Features.Set<IHttpResponseFeature>(capture);
        http.Features.Set<IHttpResponseBodyFeature>(capture);
        var callbacks = 0;
        http.Response.OnStarting(() => { http.Response.Headers[InjectedHeader] = InjectedValue; callbacks++; return Task.CompletedTask; });
        var response = new UserInfoResponse(http, Subject, null);
        await Assert.ThrowsAsync<InvalidOperationException>(() => response.FormatAsync(http));
        Assert.Equal(1, callbacks);
    }

    /// <summary>Reserved capacity is not pending output; only advanced bytes count, and successful flush/completion resets the count exactly.</summary>
    [Fact]
    public async Task Given_BoundedWriter_When_BytesAreReservedAdvancedAndFlushed_Then_PendingAccountingMatchesActualOutput()
    {
        var request = new ProtocolStorageRequest(TimeProvider.System, TestContext.Current.CancellationToken);
        using var capture = new ProtocolResponseCapture(request);
        var payload = Encoding.UTF8.GetBytes(Subject);
        var writer = capture.Writer;
        Assert.True(writer.CanGetUnflushedBytes);
        payload.CopyTo(writer.GetMemory(payload.Length));
        Assert.Equal(0, writer.UnflushedBytes);
        writer.Advance(payload.Length);
        Assert.Equal(payload.Length, writer.UnflushedBytes);
        await writer.FlushAsync(TestContext.Current.CancellationToken);
        Assert.Equal(0, writer.UnflushedBytes);
        Assert.Equal(payload, capture.Bytes.ToArray());
        await writer.CompleteAsync();
        Assert.Equal(0, writer.UnflushedBytes);
    }

    /// <summary>Failed or cancelled flushes retain pending-byte evidence and latch publication failure rather than pretending those bytes were delivered.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_PendingBytes_When_CallbackFailsOrFlushIsCancelled_Then_BytesRemainPendingAndPublicationFails(bool cancel)
    {
        var request = new ProtocolStorageRequest(TimeProvider.System, TestContext.Current.CancellationToken);
        using var capture = new ProtocolResponseCapture(request);
        using var cancellation = new CancellationTokenSource();
        var payload = Encoding.UTF8.GetBytes(Subject);
        payload.CopyTo(capture.Writer.GetMemory(payload.Length));
        capture.Writer.Advance(payload.Length);
        if (cancel)
        {
            await cancellation.CancelAsync();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => capture.Writer.FlushAsync(cancellation.Token).AsTask());
        }
        else
        {
            capture.OnStarting(_ => Task.FromException(new InvalidOperationException(InjectedValue)), new object());
            await Assert.ThrowsAsync<InvalidOperationException>(() => capture.Writer.FlushAsync(TestContext.Current.CancellationToken).AsTask());
        }
        Assert.Equal(payload.Length, capture.Writer.UnflushedBytes);
        Assert.True(capture.Bytes.IsEmpty);
        Assert.Throws<InvalidOperationException>(() => request.RequirePublication(ProtocolResponseOutcome.ReadOnly));
    }
}
