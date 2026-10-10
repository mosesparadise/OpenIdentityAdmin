using Microsoft.AspNetCore.Identity;

namespace IdentityPlatform.Infrastructure.Features.Identity.Persistence;

public sealed class PlatformUser : IdentityUser
{
    public bool IsEnabled { get; set; } = true;
}
