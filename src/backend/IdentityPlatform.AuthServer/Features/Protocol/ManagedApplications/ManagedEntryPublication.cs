using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolTransport;
using OpenIddict.Server;

namespace IdentityPlatform.AuthServer.Features.Protocol.ManagedApplications;

/// <summary>Publishes only an actual cached-request handoff and frozen issuer-local account redirect, without a native token or administrator permit.</summary>
internal sealed class ManagedEntryPublication
{
    private static readonly object Key = new();
    private readonly OpenIddictServerEvents.ProcessRequestContext outer;
    private readonly ManagedAdmission admission;
    private readonly ProtocolResponseOutput output;
    private readonly ProtocolStorageRequest request;
    private readonly long expires;

    /// <summary>Captures one private completed child output and its exact native context.</summary>
    private ManagedEntryPublication(OpenIddictServerEvents.ProcessRequestContext outer, ManagedAdmission admission, ProtocolResponseOutput output, long expires)
    {
        this.outer = outer;
        this.admission = admission;
        this.output = output;
        this.expires = expires;
        request = ProtocolStorageAmbient.Current!.Request;
    }

    /// <summary>Copies and seals the parent only after actual child cleanup has finished.</summary>
    internal static async Task PrepareAsync(HttpContext http, OpenIddictServerEvents.ProcessRequestContext outer, ManagedAdmission admission,
        ProtocolResponseOutput output, long expires)
    {
        await output.CopyAndSealParentAsync(http.RequestAborted);
        http.Items.Add(Key, new ManagedEntryPublication(outer, admission, output, expires));
    }

    /// <summary>Consumes the exact handoff permit once; cache-handled and administrator endpoint authority cannot be substituted.</summary>
    internal static bool Claim(HttpContext http, OpenIddictServerEvents.ProcessRequestContext actual)
    {
        if (!http.Items.Remove(Key, out var item))
        {
            return false;
        }
        if (item is not ManagedEntryPublication proof || !ReferenceEquals(proof.outer, actual) ||
            !ReferenceEquals(proof.request, ProtocolStorageAmbient.Current?.Request) || !actual.IsRequestSkipped || actual.IsRequestHandled ||
            actual.IsRejected || !proof.admission.HasHandoff || proof.expires <= proof.request.Clock.GetUtcNow().ToUnixTimeSeconds())
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        proof.output.RequireFrozenParent();
        proof.request.RequirePublication(ProtocolResponseOutcome.Committed);
        return true;
    }
}
