using IdentityPlatform.AuthServer.Features.Protocol.Denials;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolTransport;
using Microsoft.AspNetCore.Authentication;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using OpenIddict.Server.AspNetCore;

namespace IdentityPlatform.AuthServer.Features.Protocol.UserInfo;

/// <summary>Owns one current projection phase and publishes only after native validation, callbacks and actual transaction cleanup.</summary>
internal static class UserInfoCoordinator
{
    private static readonly object Key = new();

    /// <summary>Runs the Minimal API decision through the named native server scheme and a separate bounded current-authority read.</summary>
    internal static async Task<IResult> ReadAsync(HttpContext http)
    {
        var transaction = http.Features.Get<OpenIddictServerAspNetCoreFeature>()?.Transaction ?? throw ProtocolStorageOwner.Unavailable();
        var admission = UserInfoAdmission.Find(transaction) ?? throw ProtocolStorageOwner.Unavailable();
        admission.RequireCurrent();
        admission.Parent!.RequireEmpty();
        var authentication = await http.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        if (!authentication.Succeeded || authentication.Principal is null || !admission.Matches(authentication.Principal))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        var request = ProtocolStorageAmbient.Current!.Request;
        var result = await ProtocolResponseChild.RunAsync(http, http.RequestServices.GetRequiredService<IServiceScopeFactory>(),
            (scope, response, token) => ProjectAsync(http, transaction, admission, scope, response, token));
        admission.RequireCurrent();
        admission.Parent.RequireEmpty();
        if (!result.Result.Allowed)
        {
            return NativeErrorOrigin.Deny(http, OpenIddictConstants.Errors.InvalidToken);
        }
        var checkpoint = request.CaptureUserInfoCheckpoint(result.Result.Phase);
        var output = result.Output ?? throw ProtocolStorageOwner.Unavailable();
        await output.CopyAndSealParentAsync(request.Token);
        admission.RequireCurrent();
        http.Items.Add(Key, new Publication(transaction, request, admission, checkpoint, output));
        return Results.Empty;
    }

    /// <summary>Normal business denial rolls back and drains; storage, ownership and response-tampering failures stay exceptional.</summary>
    private static async ValueTask<ProjectionResult> ProjectAsync(HttpContext http, OpenIddictServerTransaction native, UserInfoAdmission admission,
        ProtocolStorageScope scope, ProtocolResponseChild response, CancellationToken token)
    {
        await using var transaction = await ProtocolStorageTransaction.OpenAsync(scope.Owner);
        var phase = scope.Owner.Capture(write: true);
        NativeErrorOrigin.RecordPhase(native, scope.Owner);
        var authority = await UserInfoAuthority.ReadAsync(scope, admission.Principal, token);
        if (authority is null)
        {
            return new(false, phase);
        }
        ProtocolStorageAmbient.Current!.Request.RestrictPublication(authority.Expires);
        var projection = new UserInfoResponse(http, authority.Subject, authority.Email);
        await projection.FormatAsync(http);
        await response.SealAsync();
        admission.RequireCurrent();
        projection.RequireCurrent(http);
        if (!await authority.VerifyAsync(scope, admission.Principal, token))
        {
            return new(false, phase);
        }
        await transaction.CommitAsync(token);
        return new(true, phase);
    }

    /// <summary>Consumes a private success receipt only for the exact skipped native UserInfo request and original parent response.</summary>
    internal static bool Claim(HttpContext http, OpenIddictServerEvents.ProcessRequestContext outer)
    {
        if (!http.Items.Remove(Key, out var value))
        {
            return false;
        }
        if (value is not Publication receipt || outer.EndpointType != OpenIddictServerEndpointType.UserInfo ||
            !outer.IsRequestSkipped || outer.IsRequestHandled || outer.IsRejected || !ReferenceEquals(outer.Transaction, receipt.Transaction) ||
            !ReferenceEquals(receipt.Request, ProtocolStorageAmbient.Current?.Request) || receipt.Admission.Parent?.Current() != true)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        receipt.Admission.RequireCurrent();
        receipt.Output.RequireFrozenParent();
        receipt.Request.AdmitUserInfo(receipt.Output, receipt.Checkpoint);
        return true;
    }

    /// <summary>Retains only settled phase identity after child cleanup.</summary>
    private sealed record ProjectionResult(bool Allowed, ProtocolStoragePhase Phase);
    /// <summary>Retains original native identities and immutable data only, with no live child service or provider.</summary>
    private sealed record Publication(OpenIddictServerTransaction Transaction, ProtocolStorageRequest Request, UserInfoAdmission Admission,
        ProtocolStorageCheckpoint Checkpoint, ProtocolResponseOutput Output);
}
