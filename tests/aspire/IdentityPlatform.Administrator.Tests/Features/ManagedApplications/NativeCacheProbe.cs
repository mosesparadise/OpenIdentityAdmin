using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using Microsoft.AspNetCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.EntityFrameworkCore.Models;
using OpenIddict.Server;

namespace IdentityPlatform.Administrator.Tests.Features.ManagedApplications;

/// <summary>Injects a causal late failure only after independently reading the actual native redeemed row in the current transaction.</summary>
internal sealed class NativeCacheProbe(ProtocolStorageOwner owner, NativeCacheFault fault)
    : IOpenIddictServerHandler<OpenIddictServerEvents.ProcessSignInContext>
{
    /// <summary>Counts anonymous cache sign-in without handling it or supplying a synthetic human principal.</summary>
    internal static ValueTask Observe(OpenIddictServerEvents.ProcessSignInContext context)
    {
        if (context.EndpointType == OpenIddictServerEndpointType.Authorization && string.IsNullOrEmpty(context.Request.RequestUri))
        {
            Assert.NotNull(context.Principal);
            Assert.False(context.Principal.Identity!.IsAuthenticated);
            Assert.Null(context.Principal.GetClaim(OpenIddictConstants.Claims.Subject));
            context.Transaction.GetHttpRequest()!.HttpContext.RequestServices.GetRequiredService<NativeCacheFault>().AnonymousSignIns++;
        }
        return ValueTask.CompletedTask;
    }

    /// <summary>Leaves initial caching and normal issuance intact; selected failures prove reach after real manager redemption.</summary>
    public async ValueTask HandleAsync(OpenIddictServerEvents.ProcessSignInContext context)
    {
        if (fault.Selected == NativeCacheFailure.None ||
            context.EndpointType == OpenIddictServerEndpointType.Authorization && string.IsNullOrEmpty(context.Request.RequestUri))
        {
            return;
        }
        var authentication = context.Transaction.GetProperty<OpenIddictServerEvents.ProcessAuthenticationContext>(
            typeof(OpenIddictServerEvents.ProcessAuthenticationContext).FullName!);
        var principal = context.EndpointType == OpenIddictServerEndpointType.Authorization
            ? authentication!.RequestTokenPrincipal : authentication!.AuthorizationCodePrincipal;
        var id = principal!.GetTokenId();
        Assert.False(string.IsNullOrEmpty(id));
        var status = await owner.RunAsync(token => new ValueTask<string?>(owner.Database.Set<OpenIddictEntityFrameworkCoreToken>().AsNoTracking()
            .Where(row => row.Id == id).Select(row => row.Status).SingleAsync(token)), context.CancellationToken);
        Assert.Equal(OpenIddictConstants.Statuses.Redeemed, status);
        fault.RedemptionHits++;
        if (fault.Selected == NativeCacheFailure.Throw)
        {
            throw fault.Exception;
        }
        context.Reject(OpenIddictConstants.Errors.ServerError);
    }
}

/// <summary>Keeps only finite reach counters and exact synthetic exception identity, never request or response credentials.</summary>
internal sealed class NativeCacheFault
{
    private const string Failure = "Injected after actual native cache/code redemption.";
    internal Exception Exception { get; } = new InvalidOperationException(Failure);
    internal NativeCacheFailure Selected
    {
        get; set;
    }
    internal int AnonymousSignIns
    {
        get; set;
    }
    internal int RedemptionHits
    {
        get; set;
    }

    /// <summary>Matches only the exact injected fault, including owned-cleanup aggregation, so unrelated failures cannot satisfy rollback tests.</summary>
    internal bool Caused(Exception? actual) => ReferenceEquals(actual, Exception) ||
        actual is AggregateException aggregate && aggregate.InnerExceptions.Any(Caused) || actual?.InnerException is { } inner && Caused(inner);
}

/// <summary>Distinguishes a committed native rejection from an enclosing transaction rollback.</summary>
internal enum NativeCacheFailure
{
    None, Reject, Throw
}
