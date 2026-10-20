using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using Microsoft.AspNetCore;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace IdentityPlatform.AuthServer.Features.Protocol.Administrator;

/// <summary>Restricts native-validated fixed-client requests without accepting caller-selected audiences or interactive flows.</summary>
internal sealed class AdministratorRequestPolicy :
    IOpenIddictServerHandler<OpenIddictServerEvents.ValidatePushedAuthorizationRequestContext>,
    IOpenIddictServerHandler<OpenIddictServerEvents.ValidateAuthorizationRequestContext>,
    IOpenIddictServerHandler<OpenIddictServerEvents.ValidateTokenRequestContext>
{
    internal const int AfterNativeValidation = int.MaxValue - 100_000;

    /// <summary>Retains native cryptographic validation and then checks the exact noninteractive fixed scope/profile.</summary>
    public async ValueTask HandleAsync(OpenIddictServerEvents.ValidatePushedAuthorizationRequestContext context)
    {
        if (context.ClientId == AdministratorProtocolIdentity.ClientId &&
            (!await TargetsAbsentAsync(context) || !AdministratorDecisionPolicy.AllowsPushedAuthorization(context.Request, out _)))
        {
            context.Reject(OpenIddictConstants.Errors.InvalidRequest);
        }
    }

    /// <summary>Requires a native restored PAR demand; ordinary native request caching cannot enter administrator issuance.</summary>
    public async ValueTask HandleAsync(OpenIddictServerEvents.ValidateAuthorizationRequestContext context)
    {
        if (context.ClientId == AdministratorProtocolIdentity.ClientId &&
            (!await TargetsAbsentAsync(context) || !AdministratorDecisionPolicy.AllowsAuthorization(context.Request, out _)))
        {
            context.Reject(OpenIddictConstants.Errors.InvalidRequest);
        }
    }

    /// <summary>Token exchange retains only its protected code's fixed resource and cannot accept an override, even an empty-present field.</summary>
    public async ValueTask HandleAsync(OpenIddictServerEvents.ValidateTokenRequestContext context)
    {
        if (context.ClientId == AdministratorProtocolIdentity.ClientId && (!await TargetsAbsentAsync(context) || !context.Request.IsAuthorizationCodeGrantType() ||
            context.Request.HasParameter(OpenIddictConstants.Parameters.Resource) || context.Request.HasParameter(OpenIddictConstants.Parameters.Audience)))
        {
            context.Reject(OpenIddictConstants.Errors.InvalidRequest);
        }
    }
    /// <summary>Checks original extracted HTTP collections because native SetParameter deliberately removes empty string values.</summary>
    private static async ValueTask<bool> TargetsAbsentAsync(OpenIddictServerEvents.BaseValidatingContext context)
    {
        var http = context.Transaction.GetHttpRequest();
        if (http is null)
        {
            return false;
        }
        if (http.Query.Keys.Any(IsTarget))
        {
            return false;
        }
        return !http.HasFormContentType || !(await http.ReadFormAsync(context.CancellationToken)).Keys.Any(IsTarget);
    }

    /// <summary>Rejects canonical and case-aliased target overrides before normalization can hide them.</summary>
    private static bool IsTarget(string name) => string.Equals(name, OpenIddictConstants.Parameters.Resource, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(name, OpenIddictConstants.Parameters.Audience, StringComparison.OrdinalIgnoreCase);
}
