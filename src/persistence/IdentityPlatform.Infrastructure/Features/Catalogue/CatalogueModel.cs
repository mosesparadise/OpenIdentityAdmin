using IdentityPlatform.Core.Features.Catalogue;
using IdentityPlatform.Database;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using Microsoft.EntityFrameworkCore;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Infrastructure.Features.Catalogue;

/// <summary>Composes provider-portable catalogue metadata while preserving native protocol rows and restricted reference ownership.</summary>
internal static class CatalogueModel
{
    private const string ApiAudienceRevisionConstraint = "CK_ApiAudience_Revision";
    private const string ScopePolicyRevisionConstraint = "CK_ScopePolicy_Revision";
    private const string ScopePolicyDisclosureConstraint = "CK_ScopePolicy_Disclosure";
    private const string AudienceRevision = "\"Revision\" > 0";
    private const string ScopePolicySql = "\"Kind\" IN ('identity', 'api') AND \"Disclosure\" IN ('none', 'email') AND " +
        "(\"Kind\" = 'identity' OR \"Disclosure\" = 'none')";

    /// <summary>Adds only bounded product rows and durable deletion intent through the selected provider's existing model dialect.</summary>
    internal static void Configure(ModelBuilder builder, IDatabaseModelDialect dialect)
    {
        Audience(builder, dialect);
        Scope(builder, dialect);
        CatalogueDeletionModel.Configure(builder, dialect);
    }

    /// <summary>Indexes the exact immutable protocol resource using the provider's ordinal semantics, independently of display metadata.</summary>
    private static void Audience(ModelBuilder builder, IDatabaseModelDialect dialect)
    {
        var row = builder.Entity<ApiAudience>();
        row.HasKey(value => value.Id);
        FactorSchema.Text(row, nameof(ApiAudience.Id), CatalogueRules.IdentifierLength, dialect);
        FactorSchema.Text(row, nameof(ApiAudience.Resource), CatalogueRules.ResourceLength, dialect);
        row.Property(value => value.DisplayName).HasMaxLength(CatalogueRules.DisplayLength);
        row.Property(value => value.Owner).HasMaxLength(CatalogueRules.DisplayLength);
        row.Property(value => value.Revision).IsConcurrencyToken();
        row.HasIndex(value => value.Resource).IsUnique();
        row.ToTable(nameof(ApiAudience), table => table.HasCheckConstraint(ApiAudienceRevisionConstraint, dialect.Identifiers(AudienceRevision)));
    }

    /// <summary>Keeps native scope resources authoritative and prevents deleting native rows behind live product policy.</summary>
    private static void Scope(ModelBuilder builder, IDatabaseModelDialect dialect)
    {
        var row = builder.Entity<ScopePolicy>();
        row.HasKey(value => value.ScopeId);
        // Native foreign keys inherit the unchanged parent storage type and collation; catalogue inputs retain their separate bounded identifier validation.
        FactorSchema.Text(row, nameof(ScopePolicy.Kind), CatalogueRules.Identity.Length, dialect);
        FactorSchema.Text(row, nameof(ScopePolicy.Disclosure), CatalogueRules.Email.Length, dialect);
        row.Property(value => value.ConsentText).HasMaxLength(CatalogueRules.ConsentLength);
        row.Property(value => value.Revision).IsConcurrencyToken();
        row.HasOne<OpenIddictEntityFrameworkCoreScope>().WithOne().HasForeignKey<ScopePolicy>(value => value.ScopeId).OnDelete(DeleteBehavior.Restrict);
        row.ToTable(nameof(ScopePolicy), table =>
        {
            table.HasCheckConstraint(ScopePolicyRevisionConstraint, dialect.Identifiers(AudienceRevision));
            table.HasCheckConstraint(ScopePolicyDisclosureConstraint, dialect.Identifiers(ScopePolicySql));
        });
    }
}
