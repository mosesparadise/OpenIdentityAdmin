using IdentityPlatform.AuthServer.Features.Protocol.ManagedApplications;
using Microsoft.AspNetCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Server;

namespace IdentityPlatform.Administrator.Tests.Features.ManagedApplications;

/// <summary>Runs an actual response callback inside the native formatter/seal boundary before any staged output can publish.</summary>
internal sealed class ManagedIssuanceCallback
{
    private const string ForeignState = "foreign-state";
    internal bool Armed
    {
        get; set;
    }
    internal bool Fired
    {
        get; private set;
    }

    /// <summary>Uses the genuine native request and response without replacing the formatter, capture or handler outcome.</summary>
    internal void Configure(IServiceCollection services) => services.AddOpenIddict().AddServer(server =>
        server.AddEventHandler<OpenIddictServerEvents.ProcessSignInContext>(handler => handler
            .SetOrder(OpenIddictServerHandlers.GenerateAccessToken.Descriptor.Order - 1).UseInlineHandler(HandleAsync)));

    /// <summary>Changes a native output parameter only when actual response callbacks execute, requiring final binding failure.</summary>
    private ValueTask HandleAsync(OpenIddictServerEvents.ProcessSignInContext context)
    {
        if (Armed && ManagedIssuance.Find(context.Transaction) is not null)
        {
            context.Transaction.GetHttpRequest()!.HttpContext.Response.OnStarting(() =>
            {
                context.Response.State = ForeignState;
                Fired = true;
                return Task.CompletedTask;
            });
        }
        return ValueTask.CompletedTask;
    }
}
