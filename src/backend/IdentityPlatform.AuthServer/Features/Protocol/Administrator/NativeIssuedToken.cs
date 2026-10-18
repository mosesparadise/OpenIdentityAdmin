using System.Security.Claims;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace IdentityPlatform.AuthServer.Features.Protocol.Administrator;

/// <summary>Observes actual native creation, cryptography and optional reference-payload persistence for one generated token.</summary>
internal sealed class NativeIssuedToken
{
    private readonly OpenIddictServerEvents.GenerateTokenContext context;
    private readonly ClaimsPrincipal principal;
    private readonly string type;
    private readonly bool reference;
    private string? identifier;
    private string? serialized;
    private string? returned;
    private string? binding;
    private bool payload;

    /// <summary>Requires real storage and the fixed profile's native JWT/reference policy before any handler can create output.</summary>
    internal NativeIssuedToken(OpenIddictServerEvents.GenerateTokenContext context)
    {
        this.context = context;
        principal = context.Principal;
        type = context.TokenType;
        reference = type is OpenIddictConstants.TokenTypeIdentifiers.Private.RequestToken or OpenIddictConstants.TokenTypeIdentifiers.Private.AuthorizationCode;
        if (!context.CreateTokenEntry || context.IsReferenceToken != reference || context.PersistTokenPayload != reference ||
            context.TokenFormat != OpenIddictConstants.TokenFormats.Private.JsonWebToken || context.ClientId != AdministratorProtocolIdentity.ClientId ||
            !string.IsNullOrEmpty(context.Token) || !string.IsNullOrEmpty(principal.GetTokenId()))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
    }

    /// <summary>Checks exact context and immutable native generation settings before and after every observed native handler.</summary>
    internal void RequireContext(OpenIddictServerEvents.GenerateTokenContext current)
    {
        if (!ReferenceEquals(context, current) || !ReferenceEquals(principal, current.Principal) || current.TokenType != type ||
            current.ClientId != AdministratorProtocolIdentity.ClientId || !current.CreateTokenEntry || current.PersistTokenPayload != reference ||
            current.IsReferenceToken != reference || current.TokenFormat != OpenIddictConstants.TokenFormats.Private.JsonWebToken ||
            current.IsRejected || current.IsRequestHandled || current.IsRequestSkipped)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
    }

    /// <summary>Observes exactly one actual native create and requires its newly assigned persistent identifier.</summary>
    internal async ValueTask CreateAsync(Func<ValueTask> native)
    {
        RequireContext(context);
        if (identifier is not null || !string.IsNullOrEmpty(principal.GetTokenId()))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        await native();
        RequireContext(context);
        identifier = principal.GetTokenId();
        if (string.IsNullOrEmpty(identifier))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
    }

    /// <summary>Prevents a preset token from satisfying the native crypto handler's intentional skip behavior.</summary>
    internal async ValueTask SignAsync(Func<ValueTask> native)
    {
        RequireContext(context);
        if (identifier is null || serialized is not null || !string.IsNullOrEmpty(context.Token))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        await native();
        RequireContext(context);
        serialized = context.Token;
        if (string.IsNullOrEmpty(serialized))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        binding = Binding(principal);
    }

    /// <summary>Preserves the serialized protected payload before native reference substitution and observes the actual store update.</summary>
    internal async ValueTask PersistAsync(Func<ValueTask> native)
    {
        RequireContext(context);
        if (!reference || payload || serialized is null || context.Token != serialized || binding != Binding(principal))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        await native();
        RequireContext(context);
        if (string.IsNullOrEmpty(context.Token) || context.Token == serialized)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        payload = true;
    }

    /// <summary>Freezes final native output only after the whole nested generation dispatcher returns normally.</summary>
    internal void Complete()
    {
        RequireContext(context);
        if (returned is not null || identifier is null || serialized is null || payload != reference ||
            string.IsNullOrEmpty(context.Token) || (!reference && context.Token != serialized) || binding != Binding(principal))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        returned = context.Token;
    }

    /// <summary>Checks returned native fields and exact prepared-principal identity without reconstructing library cryptography.</summary>
    internal bool Matches(ClaimsPrincipal? prepared, string? value) => returned is not null && ReferenceEquals(prepared, principal) &&
        value == returned && context.Token == returned && principal.GetTokenId() == identifier && binding == Binding(principal);

    /// <summary>Reconciles the exact created row and hashed native reference lookup inside the original issuance transaction.</summary>
    internal async ValueTask<bool> VerifyAsync(ProtocolStorageScope scope, IOpenIddictTokenManager manager, string application, CancellationToken token)
    {
        RequireContext(context);
        if (returned is null || binding != Binding(principal) || identifier != principal.GetTokenId())
        {
            return false;
        }
        var row = await manager.FindByIdAsync(identifier!, token);
        if (row is null || !await ReloadAsync(scope, row, token))
        {
            return false;
        }
        var descriptor = new OpenIddictTokenDescriptor();
        await manager.PopulateAsync(descriptor, row, token);
        if (descriptor.ApplicationId != application || descriptor.Type != type || descriptor.Status != OpenIddictConstants.Statuses.Valid ||
            descriptor.AuthorizationId != principal.GetAuthorizationId() || descriptor.Subject != principal.GetClaim(OpenIddictConstants.Claims.Subject) ||
            descriptor.CreationDate != principal.GetCreationDate() || descriptor.ExpirationDate != principal.GetExpirationDate() ||
            descriptor.ExpirationDate is null || descriptor.Payload != (reference ? serialized : null))
        {
            return false;
        }
        var resolved = reference ? await manager.FindByReferenceIdAsync(returned, token) : row;
        return resolved is not null && await manager.GetIdAsync(resolved, token) == identifier;
    }

    /// <summary>Reloads the exact tracked native row so EF identity resolution cannot hide a same-transaction persisted mutation.</summary>
    private static ValueTask<bool> ReloadAsync(ProtocolStorageScope scope, object row, CancellationToken token) => scope.Owner.RunAsync(async current =>
    {
        var entry = scope.Owner.Database.Entry(row);
        await entry.ReloadAsync(current);
        return entry.State != EntityState.Detached;
    }, token);

    /// <summary>Detects claim or destination mutation between native signing, response formatting and final persistence reconciliation.</summary>
    private static string Binding(ClaimsPrincipal value) => FactorBinding.Digest(value.Claims.Select(claim => new
    {
        claim.Type,
        claim.Value,
        claim.ValueType,
        claim.Issuer,
        claim.OriginalIssuer,
        Properties = claim.Properties.OrderBy(pair => pair.Key, StringComparer.Ordinal).ToArray()
    }).ToArray());
}
