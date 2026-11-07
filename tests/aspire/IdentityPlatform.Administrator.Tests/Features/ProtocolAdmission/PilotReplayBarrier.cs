using System.Runtime.ExceptionServices;
using System.Security.Claims;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;

/// <summary>Pauses only the first pilot exchange after actual native redemption, leaving the concurrent replay free to reach its response.</summary>
internal sealed class PilotReplayBarrier
{
    private const string Pilot = "identityplatform-pilot";
    private const int WaitSeconds = 5;
    private readonly TaskCompletionSource<string> redeemed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int entered;
    internal ClaimsPrincipal Principal { get; private set; } = null!;
    internal bool Armed
    {
        get; set;
    }

    /// <summary>Preserves the native redemption handler and inserts one test-owned observation immediately after its existing order.</summary>
    internal void Configure(IServiceCollection services) => services.AddOpenIddict().AddServer()
        .AddEventHandler<OpenIddictServerEvents.ProcessSignInContext>(builder => builder
            .SetOrder(OpenIddictServerHandlers.RedeemTokenEntry.Descriptor.Order + 1).UseInlineHandler(ObserveAsync));

    /// <summary>Captures the original validated code identity after redemption; no token or principal is modified.</summary>
    private async ValueTask ObserveAsync(OpenIddictServerEvents.ProcessSignInContext context)
    {
        if (!Armed || context.ClientId != Pilot || context.EndpointType != OpenIddictServerEndpointType.Token ||
            !context.Request.IsAuthorizationCodeGrantType())
        {
            return;
        }
        Assert.Equal(1, Interlocked.Increment(ref entered));
        var authentication = context.Transaction.GetProperty<OpenIddictServerEvents.ProcessAuthenticationContext>(
            typeof(OpenIddictServerEvents.ProcessAuthenticationContext).FullName!);
        var id = authentication?.AuthorizationCodePrincipal?.GetTokenId();
        Assert.False(string.IsNullOrEmpty(id));
        Principal = authentication!.AuthorizationCodePrincipal!.Clone();
        redeemed.SetResult(id!);
        await release.Task.WaitAsync(TimeSpan.FromSeconds(WaitSeconds), TestContext.Current.CancellationToken);
    }

    /// <summary>Waits for actual native dispatch, bounded independently of the original HTTP request lifetime.</summary>
    internal Task<string> WaitAsync() => redeemed.Task.WaitAsync(TimeSpan.FromSeconds(WaitSeconds), TestContext.Current.CancellationToken);

    /// <summary>Unblocks the joined exchange on every success/failure path without retrying it.</summary>
    internal void Release() => release.TrySetResult();

    /// <summary>Always releases and drains the real winning request, preserving the first assertion or stage failure if cleanup also fails.</summary>
    internal async Task CompleteAsync(Task<HttpResponseMessage> winner, ExceptionDispatchInfo? failure)
    {
        Release();
        try
        {
            using var response = await winner;
        }
        catch (Exception error) { failure ??= ExceptionDispatchInfo.Capture(error); }
        failure?.Throw();
    }

}
