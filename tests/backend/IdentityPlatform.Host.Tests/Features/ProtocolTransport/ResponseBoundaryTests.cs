using System.Text;
using IdentityPlatform.Host.Tests.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolTransport;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Primitives;

namespace IdentityPlatform.Host.Tests.Features.ProtocolTransport;

/// <summary>Exercises real native HTTP dispatch through staged response admission without opening a database.</summary>
public sealed class ResponseBoundaryTests
{
    private const string RedirectPath = "/synthetic-complete";
    private const string CookieName = "synthetic";
    private const string CookieValue = "admitted";
    private const string CallbackHeader = "X-Callback";
    private const string CallbackValue = "owned";
    private const string ProtocolBody = "synthetic-protocol-output";
    private const string DeniedRedirect = "/must-not-escape";
    private const string DeniedValue = "must-not-escape";
    private const string MultiHeader = "X-Synthetic";
    private const string FirstValue = "first";
    private const string SecondValue = "second";
    private const string MutatedValue = "changed";

    /// <summary>Holds cookies, redirects and stream/pipe output until native work and actual scope disposal finish.</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Given_NativeResponse_When_OwnershipCompletes_Then_OnlyAdmittedBytesAndHeadersArePublished(bool client, bool pipe)
    {
        await using var probe = new NativeDispatchProbe(client);
        using var output = new MemoryStream();
        var http = new DefaultHttpContext { RequestServices = probe.Storage.Root };
        http.Response.Body = output;
        var original = http.Features.GetRequiredFeature<IHttpResponseFeature>();
        probe.Request = async context =>
        {
            http.Response.Redirect(RedirectPath);
            http.Response.Cookies.Append(CookieName, CookieValue);
            http.Response.OnStarting(() =>
            {
                Assert.Equal(0, Assert.Single(probe.Storage.Databases).Disposals);
                http.Response.Headers[CallbackHeader] = CallbackValue;
                return Task.CompletedTask;
            });
            var bytes = Encoding.UTF8.GetBytes(ProtocolBody);
            if (pipe)
            {
                await http.Response.BodyWriter.WriteAsync(bytes, TestContext.Current.CancellationToken);
            }
            else
            {
                await http.Response.Body.WriteAsync(bytes, TestContext.Current.CancellationToken);
            }
            Assert.Empty(output.ToArray());
            Assert.Empty(original.Headers);
            context.Handle();
        };
        await ProtocolHttpBoundary.RunAsync(http, probe.Storage.Factory, probe.Storage.Clock, async (_, scope, _) =>
        {
            Assert.True(await probe.InvokeAsync(http, scope));
            return ProtocolResponseOutcome.ReadOnly;
        });
        Assert.Equal(StatusCodes.Status302Found, http.Response.StatusCode);
        Assert.Equal("/synthetic-complete", http.Response.Headers.Location);
        Assert.Contains("synthetic=admitted", http.Response.Headers.SetCookie.ToString(), StringComparison.Ordinal);
        Assert.Equal("owned", http.Response.Headers[CallbackHeader]);
        Assert.Equal("synthetic-protocol-output", Encoding.UTF8.GetString(output.ToArray()));
        Assert.Equal(1, Assert.Single(probe.Storage.Databases).Disposals);
        Assert.Same(probe.Storage.Root, http.RequestServices);
    }

    /// <summary>Rejects unknown outcomes, uncommitted writes and unverified error claims without forwarding staged success material.</summary>
    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(2, false)]
    [InlineData(2, true)]
    [InlineData(3, true)]
    public async Task Given_StagedSuccess_When_PublicationHasNoCommitEvidence_Then_NoSuccessMaterialEscapes(int outcome, bool phase)
    {
        await using var probe = new StorageRequestProbe();
        using var output = new MemoryStream();
        var http = new DefaultHttpContext { RequestServices = probe.Root };
        http.Response.Body = output;
        await ProtocolHttpBoundary.RunAsync(http, probe.Factory, probe.Clock, async (_, scope, _) =>
        {
            if (phase)
            {
                await using var transaction = scope.Owner.CreateTransaction();
            }
            http.Response.Headers.Location = DeniedRedirect;
            http.Response.Cookies.Append(CookieName, DeniedValue);
            await http.Response.WriteAsync(DeniedValue, TestContext.Current.CancellationToken);
            return (ProtocolResponseOutcome)outcome;
        });
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, http.Response.StatusCode);
        Assert.Empty(output.ToArray());
        Assert.Equal(0, http.Response.Headers.Location.Count);
        Assert.Equal(0, http.Response.Headers.SetCookie.Count);
        Assert.Equal("no-store", http.Response.Headers.CacheControl);
        Assert.Equal(1, Assert.Single(probe.Databases).Disposals);
    }

    /// <summary>Prevents retained header arrays from changing the admitted snapshot after the response has started.</summary>
    [Fact]
    public async Task Given_MultiValueHeaders_When_ReturnedArraysAreMutated_Then_StagedValuesRemainUnchanged()
    {
        await using var probe = new StorageRequestProbe();
        using var output = new MemoryStream();
        var http = new DefaultHttpContext { RequestServices = probe.Root };
        http.Response.Body = output;
        await ProtocolHttpBoundary.RunAsync(http, probe.Factory, probe.Clock, async (_, _, _) =>
        {
            http.Response.Headers[MultiHeader] = new StringValues([FirstValue, SecondValue]);
            var alias = http.Response.Headers[MultiHeader].ToArray();
            Assert.True(http.Response.Headers.TryGetValue(MultiHeader, out var found));
            var values = http.Response.Headers.Values.Single().ToArray();
            var enumerated = http.Response.Headers.Single().Value.ToArray();
            await http.Response.StartAsync(TestContext.Current.CancellationToken);
            alias[0] = found.ToArray()[0] = values[0] = enumerated[0] = MutatedValue;
            return ProtocolResponseOutcome.ReadOnly;
        });
        Assert.Equal(["first", "second"], http.Response.Headers[MultiHeader].ToArray());
    }
}
