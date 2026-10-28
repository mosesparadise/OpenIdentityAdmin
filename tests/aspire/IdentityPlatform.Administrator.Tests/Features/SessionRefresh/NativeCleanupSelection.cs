using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace IdentityPlatform.Administrator.Tests.Features.SessionRefresh;

/// <summary>Selects only metadata owned by the natively authenticated application and binds it to this exact transaction.</summary>
internal sealed class NativeCleanupSelection(IOpenIddictApplicationManager applications, IOpenIddictTokenManager tokens,
    IOpenIddictAuthorizationManager authorizations) :
    IOpenIddictServerHandler<OpenIddictServerEvents.ValidateRevocationRequestContext>
{
    /// <summary>Rejects unknown or foreign selectors without manufacturing a generic OAuth credential principal.</summary>
    public async ValueTask HandleAsync(OpenIddictServerEvents.ValidateRevocationRequestContext context)
    {
        if (!NativeCleanupAdapter.Selected(context.Request))
        {
            return;
        }
        Assert.Null(context.GenericTokenPrincipal);
        var authentication = context.Transaction.GetProperty<OpenIddictServerEvents.ProcessAuthenticationContext>(
            typeof(OpenIddictServerEvents.ProcessAuthenticationContext).FullName!);
        Assert.NotNull(authentication?.ClientAssertionPrincipal);
        var nonce = (string?)context.Request[NativeCleanupProfile.Nonce];
        if (!NativeCleanupProfile.IsRandom(context.Request.Token) || !NativeCleanupProfile.IsRandom(nonce))
        {
            context.Reject(OpenIddictConstants.Errors.InvalidRequest);
            return;
        }
        var application = await applications.FindByClientIdAsync(context.ClientId!, context.CancellationToken);
        var reference = NativeCleanupProfile.Reference(context.ClientId!, context.Request.Token!);
        var row = await tokens.FindByReferenceIdAsync(reference, context.CancellationToken);
        if (application is null || row is null ||
            await tokens.GetTypeAsync(row, context.CancellationToken) != NativeCleanupProfile.SelectorType ||
            await tokens.GetApplicationIdAsync(row, context.CancellationToken) != await applications.GetIdAsync(application, context.CancellationToken) ||
            await tokens.GetPayloadAsync(row, context.CancellationToken) is not null)
        {
            context.Reject(OpenIddictConstants.Errors.InvalidRequest);
            return;
        }
        var authorization = await tokens.GetAuthorizationIdAsync(row, context.CancellationToken);
        var family = string.IsNullOrEmpty(authorization) ? null : await authorizations.FindByIdAsync(authorization, context.CancellationToken);
        if (family is null || await authorizations.GetApplicationIdAsync(family, context.CancellationToken) !=
            await applications.GetIdAsync(application, context.CancellationToken))
        {
            context.Reject(OpenIddictConstants.Errors.InvalidRequest);
            return;
        }
        context.Transaction.SetProperty(typeof(NativeCleanupTarget).FullName!, new NativeCleanupTarget(authorization!, nonce!));
    }
}

/// <summary>Contains only the manager-selected family and caller nonce, never an access or refresh credential.</summary>
internal sealed record NativeCleanupTarget(string Authorization, string Nonce);

/// <summary>Applies manager-owned family denial inside the test host's original transaction and bounded owned scope.</summary>
internal sealed class NativeCleanupRevocation(IOpenIddictAuthorizationManager authorizations, IOpenIddictTokenManager tokens) :
    IOpenIddictServerHandler<OpenIddictServerEvents.HandleRevocationRequestContext>
{
    private const string Receipt = "NativeCleanupRevocation.completed";

    /// <summary>Includes every linked token, retains the selector tombstone and accepts exact duplicate family denial.</summary>
    public async ValueTask HandleAsync(OpenIddictServerEvents.HandleRevocationRequestContext context)
    {
        if (!NativeCleanupAdapter.Selected(context.Request))
        {
            return;
        }
        Assert.Null(context.GenericTokenPrincipal);
        var target = context.Transaction.GetProperty<NativeCleanupTarget>(typeof(NativeCleanupTarget).FullName!);
        Assert.NotNull(target);
        var authorization = await authorizations.FindByIdAsync(target.Authorization, context.CancellationToken);
        Assert.NotNull(authorization);
        Assert.True(await authorizations.TryRevokeAsync(authorization, context.CancellationToken));
        await tokens.RevokeByAuthorizationIdAsync(target.Authorization, context.CancellationToken);
        context.Transaction.SetProperty(Receipt, target);
    }

    /// <summary>Stages a nonce-bound response after native response replacement; the host releases bytes only after commit and disposal.</summary>
    internal static ValueTask ConfirmAsync(OpenIddictServerEvents.ApplyRevocationResponseContext context)
    {
        if (string.IsNullOrEmpty(context.Response.Error) && context.Transaction.GetProperty<NativeCleanupTarget>(Receipt) is { } receipt)
        {
            context.Response[NativeCleanupProfile.Confirmation] = true;
            context.Response[NativeCleanupProfile.Nonce] = receipt.Nonce;
        }
        return ValueTask.CompletedTask;
    }
}
