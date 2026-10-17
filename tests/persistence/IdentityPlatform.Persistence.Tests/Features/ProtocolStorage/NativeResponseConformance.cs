using System.Text;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolTransport;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Persistence.Tests.Features.ProtocolStorage;

/// <summary>Checks actual provider commits and rollbacks against the dormant HTTP response admission ledger.</summary>
internal static class NativeResponseConformance
{
    private const string SuccessBody = "synthetic-success";
    private const string Committed = "response-committed-scope";
    private const string RolledBack = "response-rollback-scope";

    /// <summary>Runs committed and abandoned writes through the identical provider-neutral HTTP boundary.</summary>
    internal static async Task VerifyAsync(ServiceProvider root, CancellationToken token)
    {
        await WriteAsync(root, true, token);
        await WriteAsync(root, false, token);
        var request = new ProtocolStorageRequest(root.GetRequiredService<TimeProvider>(), token);
        await request.RunAsync(root.GetRequiredService<IServiceScopeFactory>(), async (scope, cancellation) =>
        {
            var manager = scope.Services.GetRequiredService<IOpenIddictScopeManager>();
            Assert.NotNull(await manager.FindByNameAsync(Committed, cancellation));
            Assert.Null(await manager.FindByNameAsync(RolledBack, cancellation));
            return true;
        });
    }

    /// <summary>Stages a success response before native commit and observes that only a completed durable phase can publish it.</summary>
    private static async Task WriteAsync(ServiceProvider root, bool commit, CancellationToken token)
    {
        using var output = new MemoryStream();
        var http = new DefaultHttpContext { RequestServices = root, RequestAborted = token };
        http.Response.Body = output;
        await ProtocolHttpBoundary.RunAsync(http, root.GetRequiredService<IServiceScopeFactory>(), root.GetRequiredService<TimeProvider>(), async (_, scope, cancellation) =>
        {
            await using var transaction = await ProtocolStorageTransaction.OpenAsync(scope.Owner);
            await scope.Services.GetRequiredService<IOpenIddictScopeManager>().CreateAsync(new OpenIddictScopeDescriptor { Name = commit ? Committed : RolledBack }, cancellation);
            await http.Response.WriteAsync(SuccessBody, cancellation);
            Assert.Empty(output.ToArray());
            if (commit)
            {
                await transaction.CommitAsync(cancellation);
            }
            return ProtocolResponseOutcome.Committed;
        });
        Assert.Equal(commit ? StatusCodes.Status200OK : StatusCodes.Status503ServiceUnavailable, http.Response.StatusCode);
        Assert.Equal(commit ? SuccessBody : string.Empty, Encoding.UTF8.GetString(output.ToArray()));
    }
}
