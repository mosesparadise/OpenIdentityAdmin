using IdentityPlatform.AuthServer.Features.Protocol.PilotReplay;
using Microsoft.AspNetCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using OpenIddict.Server.AspNetCore;

namespace IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;

/// <summary>Names two native error-publication boundaries after independently committed replay cleanup.</summary>
public enum PilotReplayPublicationFailure
{
    Formatting, StartingCallback
}

/// <summary>Fails only the first armed native replay error, after a separate observer proves committed chain revocation.</summary>
internal sealed class PilotReplayPublicationFault(PilotReplayPublicationFailure mode)
{
    internal const string StagedHeader = "X-Pilot-Replay-Unpublished";
    private const string StagedValue = "synthetic-unpublished-replay";
    private const string Pilot = "identityplatform-pilot";
    private const string FailureMessage = "Synthetic pilot replay publication failure.";
    internal bool Armed
    {
        get; set;
    }
    internal int Reached
    {
        get; private set;
    }
    internal int Observed
    {
        get; private set;
    }
    internal int Failures
    {
        get; private set;
    }
    internal Func<CancellationToken, Task>? RequireCommitted
    {
        get; set;
    }
    private Exception Failure { get; } = new InvalidOperationException(FailureMessage);

    /// <summary>Preserves native formatting and inserts only the chosen failure immediately before its existing JSON descriptor.</summary>
    internal void Configure(IServiceCollection services) => services.AddOpenIddict().AddServer()
        .AddEventHandler<OpenIddictServerEvents.ApplyTokenResponseContext>(builder => builder
            .SetOrder(OpenIddictServerAspNetCoreHandlers.ProcessJsonResponse<OpenIddictServerEvents.ApplyTokenResponseContext>.Descriptor.Order - 1)
            .UseInlineHandler(ApplyAsync));

    /// <summary>Requires the exact native invalid-grant replay before staging a marker and selecting the actual fault boundary.</summary>
    private async ValueTask ApplyAsync(OpenIddictServerEvents.ApplyTokenResponseContext context)
    {
        if (!Armed || context.Transaction.Request is not { ClientId: Pilot } request ||
            !request.IsAuthorizationCodeGrantType())
        {
            return;
        }
        Assert.Equal(OpenIddictConstants.Errors.InvalidGrant, context.Transaction.Response?.Error);
        Assert.NotNull(PilotExchange.Current);
        Assert.Null(PilotStorageFence.Current);
        Reached++;
        Assert.Equal(1, Reached);
        var response = context.Transaction.GetHttpRequest()!.HttpContext.Response;
        response.Headers[StagedHeader] = StagedValue;
        if (mode == PilotReplayPublicationFailure.StartingCallback)
        {
            response.OnStarting(() => FailAsync(context.CancellationToken));
        }
        else
        {
            await FailAsync(context.CancellationToken);
        }
    }

    /// <summary>Suppresses the request ambient before independent database observation; a failed observer cannot be credited as the deliberate fault.</summary>
    private async Task FailAsync(CancellationToken cancellation)
    {
        Task observation;
        using (ExecutionContext.SuppressFlow())
        {
            observation = Task.Run(async () =>
            {
                Assert.Null(PilotExchange.Current);
                Assert.Null(PilotStorageFence.Current);
                await (RequireCommitted ?? throw new InvalidOperationException(FailureMessage))(cancellation);
            }, cancellation);
        }
        await observation;
        Observed++;
        Failures++;
        throw Failure;
    }
}
