using Microsoft.AspNetCore;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace IdentityPlatform.AuthServer.Features.Protocol.ManagedApplications;

/// <summary>Records complete native client-secret, redirect and PKCE validation and the actual token endpoint passthrough.</summary>
internal sealed class ManagedTokenAdmission
{
    private readonly OpenIddictServerEvents.ValidateTokenRequestContext validation;
    private readonly string binding;
    private OpenIddictServerEvents.HandleTokenRequestContext? handoff;

    internal ManagedEndpointParent? Parent
    {
        get; private set;
    }

    /// <summary>Captures only the successful actual native validation dispatcher return.</summary>
    private ManagedTokenAdmission(OpenIddictServerEvents.ValidateTokenRequestContext validation)
    {
        this.validation = validation;
        binding = ManagedConsumption.Parameters(validation.Request);
    }

    /// <summary>Finds the private validation receipt associated with this original native transaction.</summary>
    internal static ManagedTokenAdmission? Find(OpenIddictServerTransaction transaction) => transaction.GetProperty<ManagedTokenAdmission>(typeof(ManagedTokenAdmission).FullName!);

    /// <summary>Preserves native validation before recording any application endpoint authority.</summary>
    internal static async ValueTask ValidateAsync(OpenIddictServerEvents.ValidateTokenRequestContext context, Func<ValueTask> native)
    {
        await native();
        if (context.IsRejected || context.IsRequestHandled || context.IsRequestSkipped)
        {
            return;
        }
        if (!context.Request.IsAuthorizationCodeGrantType() || !ManagedDemand.Candidate(context.ClientId) || Find(context.Transaction) is not null)
        {
            context.Reject(OpenIddictConstants.Errors.InvalidRequest);
            return;
        }
        context.Transaction.SetProperty(typeof(ManagedTokenAdmission).FullName!, new ManagedTokenAdmission(context));
    }

    /// <summary>Requires normal return from the installed native passthrough; manually setting skipped never satisfies this receipt.</summary>
    internal async ValueTask ObserveAsync(OpenIddictServerEvents.HandleTokenRequestContext context, Func<ValueTask> native)
    {
        if (handoff is not null || context.IsRejected || context.IsRequestHandled || context.IsRequestSkipped ||
            !ReferenceEquals(context.Transaction, validation.Transaction))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        Parent = new ManagedEndpointParent(context.Transaction.GetHttpRequest()!.HttpContext);
        await native();
        Parent.RequireEmpty();
        handoff = context;
        if (!Current())
        {
            throw ProtocolStorageOwner.Unavailable();
        }
    }

    /// <summary>Requires original validated request identity and unchanged native parameters through eventual publication.</summary>
    internal bool Current() => !validation.IsRejected && !validation.IsRequestHandled && !validation.IsRequestSkipped &&
        handoff is { IsRequestSkipped: true, IsRejected: false, IsRequestHandled: false } &&
        ReferenceEquals(validation.Request, handoff.Request) && binding == ManagedConsumption.Parameters(validation.Request) &&
        ReferenceEquals(validation, validation.Transaction.GetProperty<OpenIddictServerEvents.ValidateTokenRequestContext>(typeof(OpenIddictServerEvents.ValidateTokenRequestContext).FullName!));
}
