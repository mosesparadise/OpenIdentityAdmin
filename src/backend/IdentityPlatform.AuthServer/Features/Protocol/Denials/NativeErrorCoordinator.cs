using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using IdentityPlatform.Infrastructure.Features.ProtocolTransport;
using Microsoft.AspNetCore;
using OpenIddict.Server;

namespace IdentityPlatform.AuthServer.Features.Protocol.Denials;

/// <summary>Formats a proven native denial in a fresh child only after the preceding write phases have actually settled.</summary>
internal sealed class NativeErrorCoordinator(IServiceScopeFactory factory, AdministratorRegistrationPolicy policy)
{
    private static readonly object Key = new();

    /// <summary>Preserves actual native error handling while separating rejected issuance output from error serialization.</summary>
    internal async ValueTask RunAsync(OpenIddictServerEvents.BaseRequestContext context)
    {
        var http = context.Transaction.GetHttpRequest()?.HttpContext ?? throw ProtocolStorageOwner.Unavailable();
        var request = ProtocolStorageAmbient.Current?.Request ?? throw ProtocolStorageOwner.Unavailable();
        var origin = NativeErrorOrigin.Find(context.Transaction) ?? throw ProtocolStorageOwner.Unavailable();
        var checkpoint = origin.Claim(context);
        var proof = new NativeErrorFormatter(context, origin.Expected, policy);
        var result = await ProtocolResponseChild.RunAsync(http, factory, async (scope, response, token) =>
        {
            context.Transaction.SetProperty(typeof(NativeErrorFormatter).FullName!, proof);
            try
            {
                await DispatchAsync(scope, context);
                origin.RequireCompleted(context, parent: false);
                await response.SealAsync();
                proof.RequireCompleted(activeCapture: true);
                return true;
            }
            finally { context.Transaction.SetProperty<NativeErrorFormatter>(typeof(NativeErrorFormatter).FullName!, null); }
        });
        origin.RequireCompleted(context);
        proof.RequireCompleted(activeCapture: false);
        var output = result.Output ?? throw ProtocolStorageOwner.Unavailable();
        await output.CopyAndSealParentAsync(context.CancellationToken);
        origin.RequireCompleted(context);
        proof.RequireCompleted(activeCapture: false);
        http.Items.Add(Key, new Publication(request, context, origin, proof, checkpoint, output));
    }

    /// <summary>Uses the actual concrete native dispatcher from the new child, retaining the exact native context type.</summary>
    private static ValueTask DispatchAsync(ProtocolStorageScope scope, OpenIddictServerEvents.BaseRequestContext context) =>
        ProtocolStorageDispatch.RunAsync(scope.Owner, () => context.CancellationToken, token => context.CancellationToken = token, () => context switch
        {
            OpenIddictServerEvents.ProcessErrorContext error => scope.Services.GetRequiredService<OpenIddictServerDispatcher>().DispatchAsync(error),
            OpenIddictServerEvents.ProcessChallengeContext challenge => scope.Services.GetRequiredService<OpenIddictServerDispatcher>().DispatchAsync(challenge),
            _ => throw ProtocolStorageOwner.Unavailable()
        });

    /// <summary>Admits one private completed native denial; an error enum or arbitrary rejected context carries no such authority.</summary>
    internal static bool Claim(HttpContext http, OpenIddictServerEvents.ProcessRequestContext outer)
    {
        if (!http.Items.Remove(Key, out var value))
        {
            return false;
        }
        if (value is not Publication receipt || !ReferenceEquals(receipt.Request, ProtocolStorageAmbient.Current?.Request) ||
            !ReferenceEquals(outer.Transaction, receipt.Context.Transaction))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        receipt.Origin.RequireCompleted(receipt.Context);
        receipt.Proof.RequireCompleted(activeCapture: false);
        receipt.Output.RequireFrozenParent();
        receipt.Request.AdmitError(receipt.Output, receipt.Checkpoint);
        return true;
    }

    /// <summary>Retains original identities and data-only output, never a live child service or database owner.</summary>
    private sealed record Publication(ProtocolStorageRequest Request, OpenIddictServerEvents.BaseRequestContext Context,
        NativeErrorOrigin Origin, NativeErrorFormatter Proof, ProtocolStorageCheckpoint Checkpoint, ProtocolResponseOutput Output);
}
