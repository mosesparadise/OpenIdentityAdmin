using System.Runtime.ExceptionServices;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Client;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Pauses a fully validated native callback before local establishment so replacement ordering cannot mask product admission.</summary>
internal sealed class BrowserCallbackPause
{
    private const int CompletionOffset = 1000;
    private const int ReadySeconds = 5;
    private readonly TaskCompletionSource reached = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Awaits test release only after genuine native validation and the product identity capture have succeeded.</summary>
    internal void Configure(IServiceCollection services) => services.AddOpenIddict().AddClient()
        .AddEventHandler<OpenIddictClientEvents.ProcessAuthenticationContext>(handler => handler
            .SetOrder(OpenIddictClientHandlers.PopulateMergedPrincipal.Descriptor.Order + CompletionOffset).UseInlineHandler(async context =>
            {
                reached.TrySetResult();
                await release.Task.WaitAsync(context.CancellationToken);
            }));

    /// <summary>Starts admission before the writer race and always releases and joins the original callback even when observation fails.</summary>
    internal async Task<HttpResponseMessage[]> RunAsync(Func<Task<HttpResponseMessage>> callback,
        Func<Func<Task<HttpResponseMessage>>, Task<HttpResponseMessage[]>> race)
    {
        var pending = callback();
        HttpResponseMessage[]? results = null;
        ExceptionDispatchInfo? failure = null;
        try
        {
            await reached.Task.WaitAsync(TimeSpan.FromSeconds(ReadySeconds), TestContext.Current.CancellationToken);
            results = await race(() =>
            {
                release.TrySetResult();
                return pending;
            });
        }
        catch (Exception error) { failure = ExceptionDispatchInfo.Capture(error); }
        release.TrySetResult();
        try
        {
            var response = await pending;
            if (results is null)
            {
                response.Dispose();
            }
        }
        catch (Exception error) when (failure is not null) { throw new AggregateException(failure.SourceException, error); }
        failure?.Throw();
        return results!;
    }
}
