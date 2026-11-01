using IdentityPlatform.AuthServer.Features.Protocol.ManagedApplications;
using IdentityPlatform.Host.Tests.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolTransport;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace IdentityPlatform.Host.Tests.Features.ManagedApplications;

/// <summary>Exercises the managed parent receipt with actual separate DI scopes and same-request response captures.</summary>
public sealed class ManagedTokenOwnershipTests
{
    private const string Header = "X-Foreign-Output";

    /// <summary>An unchanged request identifier cannot make foreign services, capture, scope or pre-sign-in output into the original parent.</summary>
    [Theory]
    [InlineData(ManagedParentDefect.Valid)]
    [InlineData(ManagedParentDefect.Services)]
    [InlineData(ManagedParentDefect.Capture)]
    [InlineData(ManagedParentDefect.Scope)]
    [InlineData(ManagedParentDefect.Headers)]
    public async Task Given_OriginalManagedHandoff_When_ParentIdentityOrOutputChanges_Then_SubstitutedParentCannotBeClaimed(ManagedParentDefect defect)
    {
        await using var probe = new StorageRequestProbe();
        var request = new ProtocolStorageRequest(probe.Clock, TestContext.Current.CancellationToken);
        var error = await Record.ExceptionAsync(() => request.RunAsync(probe.Factory, async (scope, _) =>
        {
            var http = new DefaultHttpContext { RequestServices = scope.Services };
            using var capture = new ProtocolResponseCapture(request);
            using var foreign = new ProtocolResponseCapture(request);
            http.Features.Set<IHttpResponseFeature>(capture);
            http.Features.Set<IHttpResponseBodyFeature>(capture);
            var parent = new ManagedEndpointParent(http);
            if (defect == ManagedParentDefect.Services)
            {
                http.RequestServices = probe.Root;
            }
            if (defect == ManagedParentDefect.Headers)
            {
                http.Response.Headers[Header] = bool.TrueString;
            }
            if (defect == ManagedParentDefect.Capture)
            {
                http.Features.Set<IHttpResponseFeature>(foreign);
                http.Features.Set<IHttpResponseBodyFeature>(foreign);
            }
            if (defect == ManagedParentDefect.Scope)
            {
                await ProtocolStorageChild.RunAsync(probe.Factory, (_, _) => { parent.RequireEmpty(); return ValueTask.FromResult(true); });
            }
            parent.RequireEmpty();
            return true;
        }));
        if (defect == ManagedParentDefect.Valid)
        {
            Assert.Null(error);
        }
        else
        {
            Assert.IsType<InvalidOperationException>(error);
        }
    }
}

/// <summary>Identifies independently changed parent ownership or pre-sign-in output.</summary>
public enum ManagedParentDefect
{
    Valid, Services, Capture, Scope, Headers
}
