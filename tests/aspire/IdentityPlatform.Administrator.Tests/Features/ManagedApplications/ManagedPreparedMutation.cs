using IdentityPlatform.AuthServer.Features.Protocol.ManagedApplications;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace IdentityPlatform.Administrator.Tests.Features.ManagedApplications;

/// <summary>Mutates the actual native prepared clone after lifetime bounding and before its genuine generation dispatcher.</summary>
internal sealed class ManagedPreparedMutation(string target)
{
    private const string CodeTarget = "code";
    private const string AccessTarget = "access";
    private const string IdentityTarget = "identity";
    private const string ForeignRole = "administrator";
    private const string NonceTarget = "nonce";
    private const string ForeignNonce = "foreign-nonce";
    internal bool Fired
    {
        get; private set;
    }

    /// <summary>Leaves authentication and native cloning intact, injecting only at the actual late preparation slot.</summary>
    internal void Configure(IServiceCollection services) => services.AddOpenIddict().AddServer(server =>
        server.AddEventHandler<OpenIddictServerEvents.ProcessSignInContext>(handler => handler
            .SetOrder(OpenIddictServerHandlers.GenerateAccessToken.Descriptor.Order - 1).UseInlineHandler(HandleAsync)));

    /// <summary>Adds a real management role to one prepared token; a snapshot alone would preserve and sign this forbidden value.</summary>
    private ValueTask HandleAsync(OpenIddictServerEvents.ProcessSignInContext context)
    {
        if (ManagedIssuance.Find(context.Transaction) is null || Fired)
        {
            return ValueTask.CompletedTask;
        }
        if (target == NonceTarget)
        {
            if (context.IdentityTokenPrincipal is not null)
            {
                context.Principal!.SetClaim(OpenIddictConstants.Claims.Private.Nonce, ForeignNonce);
                context.IdentityTokenPrincipal.SetClaim(OpenIddictConstants.Claims.Nonce, ForeignNonce);
                Fired = true;
            }
            return ValueTask.CompletedTask;
        }
        var principal = target switch
        {
            CodeTarget => context.AuthorizationCodePrincipal,
            AccessTarget => context.AccessTokenPrincipal,
            IdentityTarget => context.IdentityTokenPrincipal,
            _ => throw new InvalidOperationException()
        };
        if (principal is not null)
        {
            principal.SetClaim(OpenIddictConstants.Claims.Role, ForeignRole);
            Fired = principal.GetClaim(OpenIddictConstants.Claims.Role) == ForeignRole;
        }
        return ValueTask.CompletedTask;
    }
}
