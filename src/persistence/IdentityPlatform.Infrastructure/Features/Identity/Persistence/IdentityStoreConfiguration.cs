using Microsoft.AspNetCore.Identity;

namespace IdentityPlatform.Infrastructure.Features.Identity.Persistence;

public static class IdentityStoreConfiguration
{
    public static void Configure(IdentityOptions options)
    {
        options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
        options.Stores.MaxLengthForKeys = 128;
    }
}
