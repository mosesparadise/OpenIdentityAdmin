using IdentityPlatform.Host.Tests.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolTransport;
using Microsoft.AspNetCore.Http;

namespace IdentityPlatform.Host.Tests.Features.ProtocolTransport;

/// <summary>Separates the verified error disposition from its required exact frozen-output and settled-phase permit.</summary>
public sealed class VerifiedErrorBoundaryTests
{
    private static readonly byte[] Error = [1, 2, 3];

    /// <summary>An enum alone cannot publish arbitrary bytes; a sealed child plus its matching data-only checkpoint can survive root disposal.</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Given_ErrorOutcome_When_ExactOutputPermitIsRequired_Then_OnlyAdmittedSealedOutputPublishes(bool admitted, bool extraPhase)
    {
        await using var storage = new StorageRequestProbe();
        using var output = new MemoryStream();
        var http = new DefaultHttpContext { RequestServices = storage.Root, RequestAborted = TestContext.Current.CancellationToken };
        http.Response.Body = output;
        await ProtocolHttpBoundary.RunAsync(http, storage.Factory, storage.Clock, async (_, _, token) =>
        {
            var request = ProtocolStorageAmbient.Current!.Request;
            var checkpoint = request.CaptureErrorCheckpoint([]);
            var child = await ProtocolResponseChild.RunAsync(http, storage.Factory, async (_, response, cancellation) =>
            {
                http.Response.StatusCode = StatusCodes.Status400BadRequest;
                await http.Response.Body.WriteAsync(Error, cancellation);
                await response.SealAsync();
                return true;
            });
            await child.Output!.CopyAndSealParentAsync(token);
            if (admitted)
            {
                request.AdmitError(child.Output, checkpoint);
            }
            if (extraPhase)
            {
                using var lifetime = new ProtocolStorageLifetime(request.Clock, ProtocolStorageLifetime.TransactionLimit, token);
                request.Track(new ProtocolStoragePhase(lifetime));
            }
            return ProtocolResponseOutcome.VerifiedNativeError;
        });
        Assert.Equal(admitted && !extraPhase ? StatusCodes.Status400BadRequest : StatusCodes.Status503ServiceUnavailable, http.Response.StatusCode);
        Assert.Equal(admitted && !extraPhase ? Error : [], output.ToArray());
        Assert.All(storage.Databases, database => Assert.Equal(1, database.Disposals));
    }
}
