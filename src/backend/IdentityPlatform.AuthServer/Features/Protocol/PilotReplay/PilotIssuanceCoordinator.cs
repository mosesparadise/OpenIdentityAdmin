using IdentityPlatform.AuthServer.Features.Protocol.Hosting;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolTransport;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace IdentityPlatform.AuthServer.Features.Protocol.PilotReplay;

/// <summary>Runs native generation on the gate-owning context and withholds its response through commit and actual scope cleanup.</summary>
internal sealed class PilotIssuanceCoordinator(IServiceScopeFactory factory)
{
    /// <summary>Acquires a fresh serialized phase while retaining the real native authentication and sign-in contexts.</summary>
    internal async ValueTask RunAsync(OpenIddictServerEvents.ProcessSignInContext context)
    {
        var exchange = PilotExchange.Current ?? throw ProtocolStorageOwner.Unavailable();
        exchange.Begin(context);
        var continuation = new PilotContinuation(exchange, context);
        using var output = new ProtocolResponseCapture(exchange.Request);
        var generated = false;
        await PilotProtocolTransaction.RunPhaseAsync(factory, exchange.Request.Clock, async (services, token) =>
        {
            var validation = continuation.Validation(token);
            await PilotTokenValidation.ValidateAsync(services, validation, token);
            if (validation.IsRejected)
            {
                context.Reject(validation.Error, validation.ErrorDescription, validation.ErrorUri);
                return true;
            }
            if (!await PilotAuthority.IsCurrentAsync(services, continuation, OpenIddictConstants.Statuses.Valid, token))
            {
                context.Reject(OpenIddictConstants.Errors.InvalidGrant);
                return true;
            }
            generated = await GenerateAsync(exchange, services, continuation, output, token);
            return generated;
        }, context.CancellationToken);
        exchange.Complete(context, generated);
        if (generated)
        {
            await ProtocolResponseTransfer.PublishAsync(exchange.Capture, exchange.Capture, output, exchange.Request.RequireCurrent, exchange.Request.Token);
            await exchange.Capture.SealAsync();
        }
    }

    /// <summary>Keeps native handlers, response callbacks and final fresh authority checks inside the same generation transaction.</summary>
    private static async ValueTask<bool> GenerateAsync(PilotExchange exchange, IServiceProvider services,
        PilotContinuation continuation, ProtocolResponseCapture output, CancellationToken token)
    {
        var context = continuation.Context;
        var original = context.CancellationToken;
        var features = new ProtocolResponseFeatures(exchange.Http, output);
        services.GetService<ProtocolHostRequest>()?.BindContinuation(exchange.Http.RequestServices.GetRequiredService<ProtocolHostRequest>(), context);
        services.GetRequiredService<PilotGeneration>().Bind(continuation);
        features.Install();
        exchange.Http.RequestServices = services;
        exchange.Http.RequestAborted = token;
        context.CancellationToken = token;
        try
        {
            await services.GetRequiredService<OpenIddictServerDispatcher>().DispatchAsync(context);
            if (context.IsRejected)
            {
                return false;
            }
            if (!continuation.Consumed || !context.IsRequestHandled || context.IsRequestSkipped)
            {
                throw ProtocolStorageOwner.Unavailable();
            }
            features.RequireIntact(exchange.Request);
            await output.SealAsync();
            features.RequireIntact(exchange.Request);
            PilotAuthority.Refresh(services);
            if (!await PilotAuthority.IsCurrentAsync(services, continuation, OpenIddictConstants.Statuses.Redeemed, token))
            {
                throw ProtocolStorageOwner.Unavailable();
            }
            return true;
        }
        finally
        {
            try
            {
                await output.JoinCallbacksAsync();
            }
            finally { context.CancellationToken = original; features.Restore(); }
        }
    }
}
