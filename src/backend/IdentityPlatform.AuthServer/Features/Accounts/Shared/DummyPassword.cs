using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace IdentityPlatform.AuthServer.Features.Accounts;

/// <summary>Performs bounded native password work for unavailable accounts without persisting synthetic identities.</summary>
/// <example><code>dummy.Verify(submittedPassword); // Discard the result and return a generic denial.</code></example>
public sealed class DummyPassword
{
    private readonly PlatformUser user = new();
    private readonly PasswordHasher<PlatformUser> hasher;
    private readonly string hash;

    /// <summary>Creates one process-local synthetic hash using the configured native password cost.</summary>
    /// <example><code>services.AddSingleton&lt;DummyPassword&gt;();</code></example>
    public DummyPassword(IOptions<PasswordHasherOptions> options)
    {
        hasher = new PasswordHasher<PlatformUser>(options);
        hash = hasher.HashPassword(user, IdentityPlatform.Infrastructure.Features.Identity.Ceremonies.CeremonySecrets.Create());
    }

    /// <summary>Consumes the configured native verification work without granting or changing authority.</summary>
    /// <example><code>dummy.Verify(password);</code></example>
    public void Verify(string password) => _ = hasher.VerifyHashedPassword(user, hash, password);
}
