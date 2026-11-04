using IdentityPlatform.AuthServer.Features.Protocol.Hosting;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolTransport;
using OpenIddict.Server;

namespace IdentityPlatform.AuthServer.Features.Accounts.ManagedApplications;

/// <summary>Marks only the explicitly enabled managed Razor family for buffered original-deadline publication.</summary>
internal sealed class ManagedAccountEndpoint;

/// <summary>Retains account rendering, native callbacks and actual scope cleanup inside the existing owned HTTP boundary.</summary>
internal static class ManagedAccountBoundary
{
    private static readonly object Receipt = new();

    /// <summary>Buffers Razor and all cookies until real scope cleanup and original-deadline checks finish.</summary>
    internal static Task RunAsync(HttpContext http, RequestDelegate next)
    {
        ManagedResponseFreeze? frozen = null;
        var request = new ProtocolStorageRequest(http.RequestServices.GetRequiredService<TimeProvider>(), http.RequestAborted, () => frozen?.Require());
        return ProtocolHttpBoundary.RunAsync(http, http.RequestServices.GetRequiredService<IServiceScopeFactory>(), request, async (_, scope, _) =>
        {
            scope.Services.GetRequiredService<ProtocolStorageSelection>().Select(ProtocolStorageMode.Owned);
            scope.Services.GetRequiredService<ProtocolHostRequest>().Bind(http, OpenIddictServerEndpointType.Unknown, ProtocolHostMode.Owned);
            await next(http);
            var capture = http.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpResponseFeature>() as ProtocolResponseCapture ?? throw ProtocolStorageOwner.Unavailable();
            var expected = new ManagedResponseFreeze(capture);
            await capture.SealAsync();
            expected.Require();
            frozen = expected;
            return http.Items.Remove(Receipt, out var value) && ReferenceEquals(value, request)
                ? ProtocolResponseOutcome.Committed : ProtocolResponseOutcome.ReadOnly;
        });
    }

    /// <summary>Records only completion returned by the managed coordinator, never native protocol or administrator publication authority.</summary>
    internal static void Completed(HttpContext http) => http.Items[Receipt] = ProtocolStorageAmbient.Current!.Request;
}
