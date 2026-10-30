using IdentityPlatform.Infrastructure.Features.Applications;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace IdentityPlatform.AuthServer.Features.Protocol.ManagedApplications;

/// <summary>Records only completed native validation; locked cache/decision children independently recheck current persisted policy.</summary>
internal sealed class ManagedAdmission
{
    private readonly OpenIddictServerEvents.ValidateAuthorizationRequestContext validation;
    private OpenIddictServerEvents.HandleAuthorizationRequestContext? handoff;
    internal ManagedDemand Demand
    {
        get;
    }

    /// <summary>Retains exact actual validation and its immutable restored intent after native handlers return normally.</summary>
    private ManagedAdmission(OpenIddictServerEvents.ValidateAuthorizationRequestContext validation, ManagedDemand demand)
    {
        this.validation = validation;
        Demand = demand;
    }

    /// <summary>Finds only a private receipt minted around the actual native dispatcher.</summary>
    internal static ManagedAdmission? Find(OpenIddictServerTransaction transaction) =>
        transaction.GetProperty<ManagedAdmission>(typeof(ManagedAdmission).FullName!);

    /// <summary>Rejects unsupported native-valid requests before the cache pipeline can create any artifacts.</summary>
    internal static async ValueTask ValidateAsync(OpenIddictServerEvents.ValidateAuthorizationRequestContext context, Func<ValueTask> native)
    {
        await native();
        if (context.IsRejected || context.IsRequestHandled || context.IsRequestSkipped)
        {
            return;
        }
        var demand = ManagedDemand.Read(context.Request);
        if (demand is null || context.RedirectUri != demand.Callback || Find(context.Transaction) is not null)
        {
            context.Reject(OpenIddictConstants.Errors.InvalidRequest);
            return;
        }
        context.Transaction.SetProperty(typeof(ManagedAdmission).FullName!, new ManagedAdmission(context, demand));
    }

    /// <summary>Observes the actual native passthrough instead of accepting a manually skipped request as endpoint authority.</summary>
    internal async ValueTask ObservePassthroughAsync(OpenIddictServerEvents.HandleAuthorizationRequestContext context, Func<ValueTask> native)
    {
        if (handoff is not null || !ReferenceEquals(context.Transaction, validation.Transaction) || context.IsRequestHandled || context.IsRequestSkipped || context.IsRejected)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        await native();
        if (!context.IsRequestSkipped || context.IsRequestHandled || context.IsRejected || !Demand.Matches(context.Request))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        handoff = context;
    }

    /// <summary>Requires the actual completed native authorization handoff on the original restored request.</summary>
    internal bool HasHandoff => handoff is { IsRequestSkipped: true, IsRequestHandled: false, IsRejected: false } && Demand.Matches(handoff.Request);

    /// <summary>Requires original successful validation and exact currently locked native registration on both cache and resume paths.</summary>
    internal bool Allows(OpenIddictServerTransaction transaction, ManagedRegistration? registration) =>
        registration is not null && registration.ClientId == Demand.ClientId && registration.Redirects.Contains(Demand.Callback, StringComparer.Ordinal) &&
        ReferenceEquals(validation.Transaction, transaction) && ReferenceEquals(validation, transaction.GetProperty<OpenIddictServerEvents.ValidateAuthorizationRequestContext>(
            typeof(OpenIddictServerEvents.ValidateAuthorizationRequestContext).FullName!)) &&
        !validation.IsRejected && !validation.IsRequestHandled && !validation.IsRequestSkipped &&
        validation.RedirectUri == Demand.Callback && Demand.Matches(transaction.Request ?? throw ProtocolStorageOwner.Unavailable());
}
