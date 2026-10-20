using System.Security.Claims;
using IdentityPlatform.Admin.Bff.Features.Authentication.Ownership;
using OpenIddict.Abstractions;
using OpenIddict.Client;

namespace IdentityPlatform.Host.Tests.Features.AdministratorClient;

/// <summary>Preserves the original validated callback across awaited consumption work, including object-identical-value substitutions.</summary>
public sealed class NativeCallbackBindingTests
{
    private const string Code = "synthetic-code";
    private const string State = "synthetic-state";
    private const string TokenId = "synthetic-token-id";
    private const string Verifier = "synthetic-code-verifier";
    private const string Changed = "synthetic-substitution";

    /// <summary>Names independent mutable inputs that an awaited read or cleanup must not replace.</summary>
    public enum Fault
    {
        PrincipalClone, TokenId, Verifier, RequestClone, Code, State
    }

    /// <summary>Each valid baseline passes before the selected mutation, then rejects without reconstructing its expected snapshot.</summary>
    [Theory]
    [InlineData(Fault.PrincipalClone)]
    [InlineData(Fault.TokenId)]
    [InlineData(Fault.Verifier)]
    [InlineData(Fault.RequestClone)]
    [InlineData(Fault.Code)]
    [InlineData(Fault.State)]
    public void Given_ValidatedCallback_When_OriginalInputsChangeDuringRedemption_Then_FrozenBindingRejects(Fault fault)
    {
        var context = new OpenIddictClientEvents.ProcessAuthenticationContext(new OpenIddictClientTransaction
        {
            Options = new(),
            Request = new() { Code = Code, State = State }
        })
        {
            StateTokenPrincipal = new ClaimsPrincipal(new ClaimsIdentity()).SetTokenId(TokenId)
                .SetClaim(OpenIddictConstants.Claims.Private.CodeVerifier, Verifier)
        };
        var binding = new NativeCallbackBinding(context);
        binding.RequireCurrent();
        switch (fault)
        {
            case Fault.PrincipalClone:
                context.StateTokenPrincipal = context.StateTokenPrincipal.Clone(_ => true);
                break;
            case Fault.TokenId:
                context.StateTokenPrincipal.SetTokenId(Changed);
                break;
            case Fault.Verifier:
                context.StateTokenPrincipal.SetClaim(OpenIddictConstants.Claims.Private.CodeVerifier, Changed);
                break;
            case Fault.RequestClone:
                context.Request = new(context.Request.GetParameters());
                break;
            case Fault.Code:
                context.Request.Code = Changed;
                break;
            case Fault.State:
                context.Request.State = Changed;
                break;
        }
        Assert.Throws<InvalidOperationException>(binding.RequireCurrent);
    }
}
