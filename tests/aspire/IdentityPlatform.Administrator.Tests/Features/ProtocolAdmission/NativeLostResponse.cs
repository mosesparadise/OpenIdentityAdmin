using System.IO.Pipelines;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;

/// <summary>Loses a committed successful response at its original live publication boundary, before any response bytes escape.</summary>
internal sealed class NativeLostResponse(HttpContext http, IHttpResponseBodyFeature inner, Action observed) : IHttpResponseBodyFeature
{
    private readonly IOException loss = new();
    private bool lost;
    public Stream Stream => inner.Stream;
    public PipeWriter Writer => inner.Writer;

    /// <summary>Preserves native buffering controls.</summary>
    public void DisableBuffering() => inner.DisableBuffering();

    /// <summary>Requires successful post-commit publication, then aborts the actual TLS connection before native response start.</summary>
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (!lost)
        {
            Assert.False(http.Response.HasStarted);
            Assert.True(http.Response.StatusCode is StatusCodes.Status200OK or StatusCodes.Status201Created);
            observed();
            http.Abort();
            lost = true;
        }
        throw loss;
    }

    /// <summary>Recognizes only this request's deliberately observed disconnect, never an unrelated transport or cleanup exception.</summary>
    internal bool Caused(Exception error) => lost && ReferenceEquals(error, loss);

    /// <summary>Forwards unused file operations without creating an alternative synthetic response.</summary>
    public Task SendFileAsync(string path, long offset, long? count, CancellationToken cancellationToken = default) =>
        inner.SendFileAsync(path, offset, count, cancellationToken);

    /// <summary>Forwards terminal completion when reached without the deliberate start failure.</summary>
    public Task CompleteAsync() => inner.CompleteAsync();
}
