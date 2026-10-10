using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace IdentityPlatform.AuthServer.Features.Accounts.Login;

public sealed class DummyPassword
{
    private readonly PlatformUser user = new();
    private readonly PasswordHasher<PlatformUser> hasher;
    private readonly string hash;

    public DummyPassword(IOptions<PasswordHasherOptions> options)
    {
        hasher = new PasswordHasher<PlatformUser>(options);
        hash = hasher.HashPassword(user, InteractionSecrets.Create());
    }

    public void Verify(string password) => _ = hasher.VerifyHashedPassword(user, hash, password);
}
