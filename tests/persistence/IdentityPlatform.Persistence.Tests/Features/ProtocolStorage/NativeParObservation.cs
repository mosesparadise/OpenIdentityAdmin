using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace IdentityPlatform.Persistence.Tests.Features.ProtocolStorage;

internal enum NativeParOutcome
{
    NotReturned, Handled, Skipped, Unhandled, InvalidRequest, InvalidClient, InvalidTarget, OtherRejection
}

/// <summary>Retains only finite native outcome categories so a failed publication assertion can identify the prior pipeline boundary.</summary>
internal static class NativeParObservation
{
    /// <summary>Never copies native descriptions, request fields or arbitrary error strings into fixture diagnostics.</summary>
    internal static NativeParOutcome Read(OpenIddictServerEvents.ProcessRequestContext context) => context.IsRejected ? context.Error switch
    {
        OpenIddictConstants.Errors.InvalidRequest => NativeParOutcome.InvalidRequest,
        OpenIddictConstants.Errors.InvalidClient => NativeParOutcome.InvalidClient,
        OpenIddictConstants.Errors.InvalidTarget => NativeParOutcome.InvalidTarget,
        _ => NativeParOutcome.OtherRejection
    } : context.IsRequestSkipped ? NativeParOutcome.Skipped : context.IsRequestHandled ? NativeParOutcome.Handled : NativeParOutcome.Unhandled;
}
