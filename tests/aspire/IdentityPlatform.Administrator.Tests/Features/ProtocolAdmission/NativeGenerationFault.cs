using System.Security.Claims;
using Microsoft.AspNetCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using OpenIddict.Server.AspNetCore;

namespace IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;

/// <summary>Changes actual native code-generation inputs at independently observed pre-subject and pre-crypto boundaries.</summary>
internal sealed class NativeGenerationFault
{
    internal enum Kind
    {
        None, IdentityPartition, PrincipalAudience, DescriptorNonce, DescriptorClaims, DescriptorHeader, Encryption, Handler
    }
    private const string Changed = "synthetic-native-generation-change";
    private const string Header = "typ";
    internal Kind Selected
    {
        get; set;
    }
    internal int Observed
    {
        get; set;
    }

    /// <summary>Retains every installed native handler and injects changes only for the explicitly selected authorization-code control.</summary>
    internal static void Configure(OpenIddictServerBuilder server)
    {
        server.Services.AddSingleton<NativeGenerationFault>();
        server.AddEventHandler<OpenIddictServerEvents.GenerateTokenContext>(handler => handler
            .SetOrder(OpenIddictServerHandlers.Protection.AttachTokenSubject.Descriptor.Order - 1).UseInlineHandler(context => Apply(context, prepared: true)));
        server.AddEventHandler<OpenIddictServerEvents.GenerateTokenContext>(handler => handler
            .SetOrder(OpenIddictServerHandlers.Protection.GenerateIdentityModelToken.Descriptor.Order - 1).UseInlineHandler(context => Apply(context, prepared: false)));
    }

    /// <summary>Records attack reach outside production exception handling and leaves all unrelated native token types untouched.</summary>
    private static ValueTask Apply(OpenIddictServerEvents.GenerateTokenContext context, bool prepared)
    {
        var control = context.Transaction.GetHttpRequest()!.HttpContext.RequestServices.GetRequiredService<NativeGenerationFault>();
        if (context.TokenType != OpenIddictConstants.TokenTypeIdentifiers.Private.AuthorizationCode || control.Selected == Kind.None ||
            prepared != (control.Selected is Kind.IdentityPartition or Kind.PrincipalAudience))
        {
            return ValueTask.CompletedTask;
        }
        control.Observed++;
        switch (control.Selected)
        {
            case Kind.IdentityPartition:
                Partition(context.Principal);
                break;
            case Kind.PrincipalAudience:
                context.Principal.SetAudiences(Changed);
                break;
            case Kind.DescriptorNonce:
                context.SecurityTokenDescriptor.Subject!.AddClaim(new Claim(OpenIddictConstants.Claims.Private.Nonce, Changed));
                break;
            case Kind.DescriptorClaims:
                context.SecurityTokenDescriptor.Claims![OpenIddictConstants.Claims.Subject] = Changed;
                break;
            case Kind.DescriptorHeader:
                context.SecurityTokenDescriptor.AdditionalHeaderClaims = new Dictionary<string, object> { [Header] = Changed };
                break;
            case Kind.Encryption:
                context.EncryptionCredentials = null;
                break;
            case Kind.Handler:
                context.SecurityTokenHandler = new();
                break;
        }
        return ValueTask.CompletedTask;
    }

    /// <summary>Moves unchanged flattened claims to a later identity while leaving the principal object intact.</summary>
    private static void Partition(ClaimsPrincipal principal)
    {
        var identity = (ClaimsIdentity)principal.Identity!;
        var clone = identity.Clone();
        foreach (var claim in identity.Claims.ToArray())
        {
            identity.RemoveClaim(claim);
        }
        principal.AddIdentity(clone);
    }
}
