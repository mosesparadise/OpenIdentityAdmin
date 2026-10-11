using OpenIddict.Abstractions;
using OpenIddict.Server;
using static OpenIddict.Server.OpenIddictServerEvents;

namespace IdentityPlatform.AuthServer.Features.Protocol.Token;

internal static class ClientIdentityConsistency
{
    private const string Property = "IdentityPlatform.OriginalClientIdentity";

    internal static ValueTask CaptureAsync(ExtractTokenRequestContext context)
    {
        // Preserve multiplicity before native Basic extraction replaces the body identity.
        context.Transaction.SetProperty(Property, new OriginalIdentity(context.Request!.GetParameter(OpenIddictConstants.Parameters.ClientId)));
        return ValueTask.CompletedTask;
    }

    internal static ValueTask ValidateAsync(ExtractTokenRequestContext context)
    {
        var original = context.Transaction.GetProperty<OriginalIdentity>(Property)!.Parameter;
        if (original is { } parameter && !parameter.Equals(context.Request!.GetParameter(OpenIddictConstants.Parameters.ClientId)))
        {
            context.Reject(OpenIddictConstants.Errors.InvalidRequest, "The client identifiers are inconsistent.");
        }
        return ValueTask.CompletedTask;
    }

    private sealed record OriginalIdentity(OpenIddictParameter? Parameter);
}
