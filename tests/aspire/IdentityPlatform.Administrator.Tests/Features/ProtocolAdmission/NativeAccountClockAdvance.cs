using IdentityPlatform.Administrator.Tests.Fixtures;
using Microsoft.AspNetCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Server;

namespace IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;

/// <summary>Advances policy time after initial locked admission so the actual quota reservation moves its persisted watermark.</summary>
internal sealed class NativeAccountClockAdvance
{
    private const int AfterAdmissionBeforeReservation = 750;
    internal int Observed
    {
        get; private set;
    }

    /// <summary>Inserts a deterministic clock-only intervention after real admission and before the installed native generation plan.</summary>
    internal static void Configure(OpenIddictServerBuilder server)
    {
        server.Services.AddSingleton<NativeAccountClockAdvance>();
        server.AddEventHandler<OpenIddictServerEvents.ProcessSignInContext>(handler => handler
            .SetOrder(OpenIddictServerHandlers.RestoreInternalClaims.Descriptor.Order + AfterAdmissionBeforeReservation)
            .UseInlineHandler(context =>
            {
                if (context.EndpointType is OpenIddictServerEndpointType.Authorization or OpenIddictServerEndpointType.Token)
                {
                    var services = context.Transaction.GetHttpRequest()!.HttpContext.RequestServices;
                    var clock = (FactorClock)services.GetRequiredService<TimeProvider>();
                    clock.Now = clock.Now.AddSeconds(1);
                    services.GetRequiredService<NativeAccountClockAdvance>().Observed++;
                }
                return ValueTask.CompletedTask;
            }));
    }
}
