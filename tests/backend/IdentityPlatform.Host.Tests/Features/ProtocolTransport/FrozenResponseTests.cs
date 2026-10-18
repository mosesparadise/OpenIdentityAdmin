using IdentityPlatform.Host.Tests.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolTransport;
using Microsoft.AspNetCore.Http;

namespace IdentityPlatform.Host.Tests.Features.ProtocolTransport;

/// <summary>Proves immutable child output remains bound to its original parent through final publication checks.</summary>
public sealed class FrozenResponseTests
{
    private const string Redirect = "/frozen-child";
    private const string MutatedRedirect = "/changed-parent";

    /// <summary>A correct copy/seal remains publishable, while a foreign-scope check poisons the original request even when caught.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_SealedParent_When_FinalScopeChecked_Then_OnlyOriginalScopeQualifies(bool foreign)
    {
        await using var storage = new StorageRequestProbe(clock: new ControlledProbeClock());
        using var output = new MemoryStream();
        var http = new DefaultHttpContext { RequestServices = storage.Root };
        http.Response.Body = output;
        var checkedOutput = false;
        await ProtocolHttpBoundary.RunAsync(http, storage.Factory, storage.Clock, async (_, _, token) =>
        {
            var result = await ProtocolResponseChild.RunAsync(http, storage.Factory, async (_, response, _) =>
            {
                http.Response.Redirect(Redirect);
                await response.SealAsync();
                return true;
            });
            await result.Output!.CopyAndSealParentAsync(token);
            if (foreign)
            {
                await Assert.ThrowsAsync<InvalidOperationException>(async () => await ProtocolStorageChild.RunAsync(storage.Factory, (_, _) =>
                {
                    result.Output.RequireFrozenParent();
                    return ValueTask.FromResult(true);
                }));
            }
            else
            {
                result.Output.RequireFrozenParent();
            }
            checkedOutput = true;
            return ProtocolResponseOutcome.ReadOnly;
        });
        Assert.True(checkedOutput);
        Assert.Equal(foreign ? StatusCodes.Status503ServiceUnavailable : StatusCodes.Status302Found, http.Response.StatusCode);
        Assert.Equal(foreign ? string.Empty : Redirect, http.Response.Headers.Location.ToString());
        Assert.Empty(output.ToArray());
    }

    /// <summary>Parent callbacks cannot alter already committed child output before the publication permit is created.</summary>
    [Fact]
    public async Task Given_SealedChild_When_ParentCallbackMutatesOutput_Then_NoAlteredResponseEscapes()
    {
        await using var storage = new StorageRequestProbe(clock: new ControlledProbeClock());
        using var output = new MemoryStream();
        var http = new DefaultHttpContext { RequestServices = storage.Root };
        http.Response.Body = output;
        var rejected = false;
        var callback = false;
        await ProtocolHttpBoundary.RunAsync(http, storage.Factory, storage.Clock, async (_, _, token) =>
        {
            http.Response.OnStarting(() => { http.Response.Headers.Location = MutatedRedirect; callback = true; return Task.CompletedTask; });
            var result = await ProtocolResponseChild.RunAsync(http, storage.Factory, async (_, response, _) =>
            {
                http.Response.Redirect(Redirect);
                await response.SealAsync();
                return true;
            });
            await Assert.ThrowsAsync<InvalidOperationException>(() => result.Output!.CopyAndSealParentAsync(token));
            rejected = true;
            return ProtocolResponseOutcome.ReadOnly;
        });
        Assert.True(callback);
        Assert.True(rejected);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, http.Response.StatusCode);
        Assert.Empty(http.Response.Headers.Location.ToString());
        Assert.Empty(output.ToArray());
    }
}
