using System.Text;
using IdentityPlatform.Administrator.Tests.Features.Applications;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace IdentityPlatform.AppHost.Tests.Features.LocalDemo;

/// <summary>Calibrates final-response date evidence with real stream forwarding and deliberately invalid publication ledgers.</summary>
public sealed class ApplicationPublicationTests
{
    private const string Route = "/bff/api/v1/applications/test/secret-replacements";
    private const string OtherRoute = "/unrelated";
    private const string Query = "?unexpected=true";
    private const string Post = "POST";
    private const string ApplicationField = "\"application\":";
    private const string ExpiresField = "\"expiresAt\":";
    private const string OriginalText = "2030-01-01T00:01:00+00:00";
    private const string LaterText = "2030-01-01T00:05:00+00:00";
    private const string Body = "{\"application\":{\"expiresAt\":\"2030-01-01T00:05:00+00:00\",\"credential\":{\"expiresAt\":\"2030-01-01T00:30:00+00:00\"}},\"clientSecret\":\"private-publication-canary\"}";
    private const string CredentialText = "2030-01-01T00:30:00+00:00";
    private const string Invalid = "not-json-private-canary";
    private const string EmptyObject = "{}";
    private const string DuplicateObject = "{},";
    private const string DuplicateNull = "null,";
    private const int MaximumBytes = 65536;
    private const int RepeatedWrites = 2;
    private const int Success = 200;
    private static readonly DateTimeOffset Original = new(2030, 1, 1, 0, 1, 0, TimeSpan.Zero);

    /// <summary>Successful publication forwards identical bytes and the original token once, and restores the server-owned feature without closing its stream.</summary>
    [Fact, Trait("Category", "Harness")]
    public async Task Given_ValidPublication_When_Observed_Then_BytesTokenAndOriginalFeatureArePreserved()
    {
        using var inner = new WriteStream();
        var http = Context(inner);
        var feature = http.Features.Get<IHttpResponseBodyFeature>();
        var dates = new ApplicationResponseDates();
        using var token = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var bytes = Encoding.UTF8.GetBytes(Body);
        using (new ApplicationResponseWrite(http, dates))
        {
            await http.Response.Body.WriteAsync(bytes, token.Token);
        }

        Assert.True(inner.Calls == 1 && inner.Token == token.Token && inner.ToArray().AsSpan().SequenceEqual(bytes));
        Assert.Same(feature, http.Features.Get<IHttpResponseBodyFeature>());
        Assert.True(inner.CanWrite && dates.LaterThan(Original));
    }

    /// <summary>Malformed, oversized, duplicated, missing or non-later metadata cannot credit evidence and never changes forwarded response bytes.</summary>
    [Fact, Trait("Category", "Harness")]
    public async Task Given_InvalidPublicationMetadata_When_Forwarded_Then_ResponseIsUnchangedAndEvidenceIsRejected()
    {
        foreach (var body in new[] { Invalid, Body + new string(' ', MaximumBytes), EmptyObject,
            Body.Replace(ApplicationField, ApplicationField + DuplicateObject + ApplicationField, StringComparison.Ordinal),
            Body.Replace(ExpiresField, ExpiresField + DuplicateNull + ExpiresField, StringComparison.Ordinal),
            Body.Replace(LaterText, OriginalText, StringComparison.Ordinal), Body.Replace(CredentialText, OriginalText, StringComparison.Ordinal) })
        {
            using var inner = new WriteStream();
            var http = Context(inner);
            var dates = new ApplicationResponseDates();
            var bytes = Encoding.UTF8.GetBytes(body);
            using (new ApplicationResponseWrite(http, dates))
            {
                await http.Response.Body.WriteAsync(bytes, TestContext.Current.CancellationToken);
            }

            Assert.True(inner.Calls == 1 && inner.ToArray().AsSpan().SequenceEqual(bytes));
            Assert.False(dates.LaterThan(Original));
        }
    }

    /// <summary>Original write failures and cancellation propagate unchanged; a later failed or additional write invalidates previously observed dates.</summary>
    [Fact, Trait("Category", "Harness")]
    public async Task Given_FailedOrRepeatedWrites_When_Observed_Then_NoPublicationEvidenceIsCredited()
    {
        foreach (var failure in new Exception[] { new IOException(), new OperationCanceledException() })
        {
            await RequireFailureAsync(failure, prior: false);
            await RequireFailureAsync(failure, prior: true);
        }
        await RequireCancellationAsync();
        using var repeated = new WriteStream();
        var context = Context(repeated);
        var observed = new ApplicationResponseDates();
        using var writes = new ApplicationResponseWrite(context, observed);
        await context.Response.Body.WriteAsync(Encoding.UTF8.GetBytes(Body), TestContext.Current.CancellationToken);
        await context.Response.Body.WriteAsync(Encoding.UTF8.GetBytes(Body), TestContext.Current.CancellationToken);
        Assert.True(repeated.Calls == RepeatedWrites && !observed.LaterThan(Original));
    }

    /// <summary>Final dates require the same exact request, both actual completion signals and a valid seal; wrong route, queries and duplicate attempts never qualify.</summary>
    [Fact, Trait("Category", "Harness")]
    public async Task Given_ResponseDates_When_AttemptOrCompletionBindingIsIncomplete_Then_FinalEvidenceFailsClosed()
    {
        foreach (var invalid in new[] { false, true })
        {
            using var inner = new WriteStream();
            var http = Context(inner);
            using var owner = new ApplicationRequestCompletion(Route, () => { }, observeDates: true);
            http.Request.Path = OtherRoute;
            Assert.Null(owner.Enter(http));
            http.Request.Path = Route;
            if (invalid)
            {
                http.Request.QueryString = new(Query);
            }

            var attempt = Assert.IsType<ApplicationRequestCompletion.Attempt>(owner.Enter(http));
            using (attempt.ObserveBody(http))
            {
                await http.Response.Body.WriteAsync(Encoding.UTF8.GetBytes(Body), TestContext.Current.CancellationToken);
            }

            Assert.False(owner.HasLaterExpiries(Original));
            attempt.PipelineEnded(Success, true, false);
            Assert.False(owner.HasLaterExpiries(Original));
            attempt.ResponseEnded();
            Assert.False(owner.HasLaterExpiries(Original));
            Assert.Equal(!invalid, owner.Seal());
            Assert.Equal(!invalid, owner.HasLaterExpiries(Original));
            _ = owner.Enter(http);
            Assert.False(owner.HasLaterExpiries(Original));
        }
    }

    /// <summary>Checks original failure identity and restoration both before any output and after one previously valid write.</summary>
    private static async Task RequireFailureAsync(Exception failure, bool prior)
    {
        using var inner = new WriteStream();
        var http = Context(inner);
        var feature = http.Features.Get<IHttpResponseBodyFeature>();
        var dates = new ApplicationResponseDates();
        using (new ApplicationResponseWrite(http, dates))
        {
            if (prior)
            {
                await http.Response.Body.WriteAsync(Encoding.UTF8.GetBytes(Body), TestContext.Current.CancellationToken);
                Assert.True(dates.LaterThan(Original));
            }
            var length = inner.Length;
            inner.Failure = failure;
            var actual = await Record.ExceptionAsync(() => http.Response.Body.WriteAsync(Encoding.UTF8.GetBytes(Body), TestContext.Current.CancellationToken).AsTask());
            Assert.Same(failure, actual);
            Assert.True(inner.Calls == (prior ? RepeatedWrites : 1) && inner.Length == length && !dates.LaterThan(Original));
        }
        Assert.Same(feature, http.Features.Get<IHttpResponseBodyFeature>());
        Assert.True(inner.CanWrite);
    }

    /// <summary>Forwards a genuinely canceled original token to the stream and cannot credit bytes or dates from that failed operation.</summary>
    private static async Task RequireCancellationAsync()
    {
        using var inner = new WriteStream();
        var http = Context(inner);
        var feature = http.Features.Get<IHttpResponseBodyFeature>();
        var dates = new ApplicationResponseDates();
        using var canceled = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        await canceled.CancelAsync();
        using (new ApplicationResponseWrite(http, dates))
        {
            var failure = await Record.ExceptionAsync(() => http.Response.Body.WriteAsync(Encoding.UTF8.GetBytes(Body), canceled.Token).AsTask());
            Assert.Equal(canceled.Token, Assert.IsAssignableFrom<OperationCanceledException>(failure).CancellationToken);
            Assert.True(inner.Token == canceled.Token && inner.Calls == 1 && inner.Length == 0 && !dates.LaterThan(Original));
        }
        Assert.Same(feature, http.Features.Get<IHttpResponseBodyFeature>());
        Assert.True(inner.CanWrite);
    }

    /// <summary>Creates a synthetic request owner for ledger calibration, without claiming network or browser execution.</summary>
    private static DefaultHttpContext Context(Stream stream)
    {
        var http = new DefaultHttpContext();
        http.Request.Path = Route;
        http.Request.Method = Post;
        http.Response.Body = stream;
        return http;
    }

    /// <summary>Records the actual forwarded operation and can fail it before accepting any bytes.</summary>
    private sealed class WriteStream : MemoryStream
    {
        internal int Calls
        {
            get; private set;
        }
        internal CancellationToken Token
        {
            get; private set;
        }
        internal Exception? Failure
        {
            get; set;
        }
        /// <summary>Retains only operation metadata, preserving the injected failure identity and normal successful write.</summary>
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            Calls++;
            Token = cancellationToken;
            return Failure is null ? base.WriteAsync(buffer, cancellationToken) : ValueTask.FromException(Failure);
        }
    }
}
