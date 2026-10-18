using System.Security.Claims;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using Microsoft.IdentityModel.Tokens;

namespace IdentityPlatform.Infrastructure.Features.ProtocolStorage;

/// <summary>Freezes the actual IdentityModel descriptor consumed by native crypto without retaining printable credential-bearing snapshots.</summary>
internal sealed class ProtocolTokenDescriptorSnapshot(SecurityTokenDescriptor descriptor)
{
    private readonly ClaimsIdentity? subject = descriptor.Subject;
    private readonly SigningCredentials? signing = descriptor.SigningCredentials;
    private readonly EncryptingCredentials? encryption = descriptor.EncryptingCredentials;
    private readonly string fields = Fields(descriptor);

    /// <summary>Requires the exact descriptor, subject and credential objects plus unchanged data used by IdentityModel.</summary>
    internal void RequireCurrent(SecurityTokenDescriptor current)
    {
        if (!ReferenceEquals(descriptor, current) || !ReferenceEquals(subject, current.Subject) ||
            !ReferenceEquals(signing, current.SigningCredentials) || !ReferenceEquals(encryption, current.EncryptingCredentials) || fields != Fields(current))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
    }

    /// <summary>Rejects preset descriptor fields before the installed native preparation handlers begin.</summary>
    internal static void RequireEmpty(SecurityTokenDescriptor descriptor)
    {
        if (Fields(descriptor) != Fields(new SecurityTokenDescriptor()) || descriptor.SigningCredentials is not null || descriptor.EncryptingCredentials is not null)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
    }

    /// <summary>Includes every pinned descriptor property that influences signed/encrypted JWT output, without exporting key material.</summary>
    private static string Fields(SecurityTokenDescriptor value) => FactorBinding.Digest(new
    {
        value.Audience,
        Audiences = value.Audiences.ToArray(),
        value.CompressionAlgorithm,
        value.Expires,
        value.IssuedAt,
        value.Issuer,
        value.NotBefore,
        value.TokenType,
        value.IncludeKeyIdInHeader,
        Claims = value.Claims?.OrderBy(pair => pair.Key, StringComparer.Ordinal).ToArray(),
        Headers = value.AdditionalHeaderClaims?.OrderBy(pair => pair.Key, StringComparer.Ordinal).ToArray(),
        InnerHeaders = value.AdditionalInnerHeaderClaims?.OrderBy(pair => pair.Key, StringComparer.Ordinal).ToArray(),
        Subject = Identity(value.Subject),
        Signing = value.SigningCredentials is { } signing ? new
        {
            signing.Algorithm,
            signing.Digest,
            signing.Key.KeyId
        } : null,
        Encryption = value.EncryptingCredentials is { } encryption ? new
        {
            encryption.Alg,
            encryption.Enc,
            encryption.Key.KeyId
        } : null
    });

    /// <summary>Captures ordered identity, actor and claim semantics, including which identity native crypto selects as its subject.</summary>
    private static object? Identity(ClaimsIdentity? identity, bool omitIdentifier = false) => identity is null ? null : new
    {
        identity.AuthenticationType,
        identity.NameClaimType,
        identity.RoleClaimType,
        Actor = Identity(identity.Actor, omitIdentifier),
        Claims = identity.Claims.Where(claim => !omitIdentifier || claim.Type != OpenIddict.Abstractions.OpenIddictConstants.Claims.Private.TokenId)
            .Select(claim => new
            {
                claim.Type,
                claim.Value,
                claim.ValueType,
                claim.Issuer,
                claim.OriginalIssuer,
                Properties = claim.Properties.OrderBy(pair => pair.Key, StringComparer.Ordinal).ToArray()
            }).ToArray()
    };

    /// <summary>Hashes every prepared identity, optionally excluding only the identifier the actual native row creator adds.</summary>
    internal static string Claims(ClaimsPrincipal principal, bool omitIdentifier = false) =>
        FactorBinding.Digest(principal.Identities.Select(identity => Identity(identity, omitIdentifier)).ToArray());
}
