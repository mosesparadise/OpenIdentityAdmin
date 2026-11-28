using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using Microsoft.AspNetCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace IdentityPlatform.Administrator.Tests.Features.ManagedApplications;

/// <summary>Advances the fixture clock past an assertion's deadline during disposal of the original HTTPS request's parent service scope.</summary>
internal sealed class KeyedPublicationDeadline : TimeProvider
{
    private const int ElapsedSeconds = 61;
    private int armed;
    private int offset;
    internal bool Advanced => Volatile.Read(ref offset) == ElapsedSeconds;

    /// <summary>Allows only the next signed machine request to select its original HTTP service scope for the clock transition.</summary>
    internal void Arm() => Volatile.Write(ref armed, 1);

    /// <summary>Follows system UTC for admission and signing until the original parent has finished producing frozen response bytes.</summary>
    public override DateTimeOffset GetUtcNow() => TimeProvider.System.GetUtcNow().AddSeconds(Volatile.Read(ref offset));

    /// <summary>Observes the installed dispatcher after token extraction and captures only its genuine parent HTTP ownership.</summary>
    internal void Configure(IServiceCollection services)
    {
        services.AddScoped<Completion>();
        services.AddOpenIddict().AddServer(server => server.AddEventHandler<OpenIddictServerEvents.ProcessRequestContext>(handler => handler
            .SetOrder(OpenIddictServerHandlers.Exchange.ExtractTokenRequest.Descriptor.Order + 1).UseInlineHandler(context =>
            {
                if (context.EndpointType == OpenIddictServerEndpointType.Token && context.Transaction.Request?.IsClientCredentialsGrantType() == true &&
                    context.Transaction.Request.ClientAssertion is not null && Interlocked.Exchange(ref armed, 0) == 1)
                {
                    var parent = context.Transaction.GetHttpRequest()!.HttpContext.RequestServices;
                    Assert.Same(parent, ProtocolStorageAmbient.Current!.Scope.Services);
                    parent.GetRequiredService<Completion>().Finish = () => Volatile.Write(ref offset, ElapsedSeconds);
                }
                return ValueTask.CompletedTask;
            })));
    }

    /// <summary>Runs once when the actual parent scope is released, after native issuance has committed.</summary>
    private sealed class Completion : IDisposable
    {
        internal Action? Finish
        {
            get; set;
        }

        /// <summary>Moves only the fixture clock during disposal.</summary>
        public void Dispose() => Finish?.Invoke();
    }
}
