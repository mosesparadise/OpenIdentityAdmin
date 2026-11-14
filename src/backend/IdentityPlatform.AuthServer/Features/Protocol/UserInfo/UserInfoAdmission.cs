using System.Security.Claims;
using IdentityPlatform.AuthServer.Features.Protocol.Administrator;
using IdentityPlatform.AuthServer.Features.Protocol.ManagedApplications;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using Microsoft.AspNetCore;
using OpenIddict.Server;

namespace IdentityPlatform.AuthServer.Features.Protocol.UserInfo;

/// <summary>Retains actual completed native access-token validation and passthrough, independently of application projection.</summary>
internal sealed class UserInfoAdmission
{
    private readonly OpenIddictServerEvents.ValidateUserInfoRequestContext validation;
    private readonly ClaimsPrincipal principal;
    private readonly string claims;
    private readonly string parameters;
    private OpenIddictServerEvents.HandleUserInfoRequestContext? handoff;
    internal ManagedEndpointParent? Parent
    {
        get; private set;
    }
    internal ClaimsPrincipal Principal => principal;

    /// <summary>Freezes the real successful native validation object and its exact validated principal before passthrough.</summary>
    private UserInfoAdmission(OpenIddictServerEvents.ValidateUserInfoRequestContext context)
    {
        validation = context;
        principal = context.AccessTokenPrincipal ?? throw ProtocolStorageOwner.Unavailable();
        claims = Claims(principal);
        parameters = ManagedConsumption.Parameters(context.Request);
    }

    /// <summary>Accepts only an equivalent principal returned by the named native authentication handler.</summary>
    internal bool Matches(ClaimsPrincipal principal) => claims == Claims(principal);

    /// <summary>Finds only the private actual-dispatch receipt on the original native transaction.</summary>
    internal static UserInfoAdmission? Find(OpenIddictServerTransaction transaction) =>
        transaction.GetProperty<UserInfoAdmission>(typeof(UserInfoAdmission).FullName!);

    /// <summary>Records authority only after all installed native validators have returned successfully.</summary>
    internal static async ValueTask ValidateAsync(OpenIddictServerEvents.ValidateUserInfoRequestContext context, Func<ValueTask> native)
    {
        await native();
        if (context.IsRejected || context.IsRequestHandled || context.IsRequestSkipped)
        {
            return;
        }
        if (Find(context.Transaction) is not null)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        context.Transaction.SetProperty(typeof(UserInfoAdmission).FullName!, new UserInfoAdmission(context));
    }

    /// <summary>Observes the installed native handoff and retains its separate one-use challenge receipt.</summary>
    internal async ValueTask ObserveAsync(OpenIddictServerEvents.HandleUserInfoRequestContext context, Func<ValueTask> native)
    {
        if (handoff is not null || !ReferenceEquals(context.Transaction, validation.Transaction) ||
            !ReferenceEquals(context.AccessTokenPrincipal, principal))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        Parent = new(context.Transaction.GetHttpRequest()!.HttpContext);
        await AdministratorPassthrough.ObserveAsync(context, native);
        Parent.RequireEmpty();
        handoff = context;
        RequireCurrent();
    }

    /// <summary>Rejects replaced validation, parameters, principal or handoff through the final publication boundary.</summary>
    internal void RequireCurrent()
    {
        if (validation.IsRejected || validation.IsRequestHandled || validation.IsRequestSkipped ||
            handoff is not { IsRequestSkipped: true, IsRequestHandled: false, IsRejected: false } ||
            !ReferenceEquals(validation.AccessTokenPrincipal, principal) || !ReferenceEquals(handoff.AccessTokenPrincipal, principal) ||
            !ReferenceEquals(validation.Request, handoff.Request) || claims != Claims(principal) || parameters != ManagedConsumption.Parameters(validation.Request) ||
            !ReferenceEquals(validation, validation.Transaction.GetProperty<OpenIddictServerEvents.ValidateUserInfoRequestContext>(typeof(OpenIddictServerEvents.ValidateUserInfoRequestContext).FullName!)))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
    }

    /// <summary>Detects changes to identities, delegation and claim values/types/destinations without printable protocol data.</summary>
    private static string Claims(ClaimsPrincipal value) => FactorBinding.Digest(value.Identities.Select(identity => new
    {
        identity.AuthenticationType,
        identity.NameClaimType,
        identity.RoleClaimType,
        Actor = identity.Actor is not null,
        Claims = identity.Claims.Select(claim => new
        {
            claim.Type,
            claim.Value,
            claim.ValueType,
            claim.Issuer,
            claim.OriginalIssuer,
            Properties = claim.Properties.OrderBy(pair => pair.Key, StringComparer.Ordinal).ToArray()
        }).ToArray()
    }).ToArray());
}
