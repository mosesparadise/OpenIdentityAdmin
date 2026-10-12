using System.Globalization;
using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;

namespace IdentityPlatform.Infrastructure.Features.Identity.Factors;

/// <summary>Binds protected material to the installation and exact owner, generation and material purpose.</summary>
internal sealed class FactorProtection(IDataProtectionProvider provider, FactorProtectionOptions options)
{
    internal const string Active = "active";
    internal const string Staged = "staged";
    internal const string Replay = "replay";
    private const string Product = "IdentityPlatform.AdministratorFactors";
    private const string Version = "v1";

    /// <summary>Encrypts material using purpose isolation; staged material also requires its persisted interaction identity.</summary>
    internal string Protect(string value, string userId, long generation, string kind, string? interaction = null) =>
        Protector(userId, generation, kind, interaction).Protect(value);

    /// <summary>Fails with a fixed diagnostic for corrupt, missing-key or substituted protected material.</summary>
    internal string Unprotect(string value, string userId, long generation, string kind, string? interaction = null)
    {
        try
        {
            return Protector(userId, generation, kind, interaction).Unprotect(value);
        }
        catch (CryptographicException)
        {
            throw new FactorUnavailableException();
        }
    }

    /// <summary>Creates explicit purpose segments so owner and generation boundaries cannot be concatenation-ambiguous.</summary>
    private IDataProtector Protector(string userId, long generation, string kind, string? interaction)
    {
        if (string.IsNullOrEmpty(userId) || generation <= 0 || kind is not (Active or Staged or Replay) ||
            (kind == Staged ? string.IsNullOrEmpty(interaction) : interaction is not null))
        {
            throw new FactorUnavailableException();
        }
        var protector = provider.CreateProtector(Product, Version, options.Issuer.AbsoluteUri, userId,
            kind, generation.ToString(CultureInfo.InvariantCulture));
        return interaction is null ? protector : protector.CreateProtector(interaction);
    }
}

/// <summary>Holds the validated canonical issuer; never creates its own key ring.</summary>
internal sealed class FactorProtectionOptions(Uri issuer)
{
    internal Uri Issuer { get; } = issuer;
}
