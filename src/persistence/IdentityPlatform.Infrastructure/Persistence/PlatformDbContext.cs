using IdentityPlatform.Database;
using IdentityPlatform.Infrastructure.Features.Applications;
using IdentityPlatform.Infrastructure.Features.Identity.BrowserCompletion;
using IdentityPlatform.Infrastructure.Features.Identity.ManagedApplications;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Infrastructure.Persistence;

/// <summary>Owns native identity/protocol persistence and transactionally related product state.</summary>
/// <example><code>await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);</code></example>
public sealed class PlatformDbContext(DbContextOptions<PlatformDbContext> options)
    : IdentityDbContext<PlatformUser>(ProviderOptions.RequireStore(options, DatabaseStore.Platform))
{
    /// <summary>Composes native library mappings with explicit bounded feature models and ordinal protocol identifiers.</summary>
    /// <example><code>services.AddIdentityPersistence(provider, runtimeConnection); // Builds these mappings through EF.</code></example>
    protected override void OnModelCreating(ModelBuilder builder)
    {
        const string PlatformSchema = "platform";
        base.OnModelCreating(builder);
        builder.HasDefaultSchema(PlatformSchema);
        ApplicationPolicyModel.Configure(builder, ProviderOptions.Provider(options).Model);
        ManagedLoginModel.Configure(builder, ProviderOptions.Provider(options).Model);
        IssuerContinuationModel.Configure(builder, ProviderOptions.Provider(options).Model);
        LoginModelConfiguration.Configure(builder, ProviderOptions.Provider(options).Model);
        AdministratorModelConfiguration.Configure(builder, ProviderOptions.Provider(options).Model);
        FactorModelConfiguration.Configure(builder, ProviderOptions.Provider(options).Model);
        AdministratorProtocolModel.ConfigurePlatform(builder, ProviderOptions.Provider(options).Model);

        if (ProviderOptions.Provider(options).Model.IdentityOrdinalCollation is { } ordinal)
        {
            builder.Entity<OpenIddictEntityFrameworkCoreApplication>()
                .Property(application => application.ClientId).UseCollation(ordinal);
            builder.Entity<OpenIddictEntityFrameworkCoreScope>()
                .Property(scope => scope.Name).UseCollation(ordinal);
        }
    }
}
