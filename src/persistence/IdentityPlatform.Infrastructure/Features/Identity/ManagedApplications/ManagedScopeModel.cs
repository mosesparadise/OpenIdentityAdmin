using IdentityPlatform.Core.Features.Catalogue;
using IdentityPlatform.Database;
using IdentityPlatform.Infrastructure.Features.Catalogue;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using Microsoft.EntityFrameworkCore;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Infrastructure.Features.Identity.ManagedApplications;

/// <summary>Adds nullable legacy-compatible policy bindings and restrictive indexed retained references without rewriting prior interactions.</summary>
internal static class ManagedScopeModel
{
    private const string PairConstraint = "CK_ManagedLoginInteraction_Policy";
    private const string PairSql = "(\"PolicySnapshot\" IS NULL AND \"PolicyDigest\" IS NULL) OR " +
        "(\"PolicySnapshot\" IS NOT NULL AND \"PolicyDigest\" IS NOT NULL)";

    /// <summary>Maps snapshot bounds and foreign keys using existing provider semantics; native scope keys inherit the native parent storage type.</summary>
    internal static void Configure(ModelBuilder builder, IDatabaseModelDialect dialect)
    {
        var interaction = builder.Entity<ManagedLoginInteraction>();
        interaction.Property(value => value.PolicySnapshot).HasMaxLength(CatalogueRules.MaximumDecisionBytes);
        FactorSchema.Text(interaction, nameof(ManagedLoginInteraction.PolicyDigest), FactorSchema.DigestLength, dialect);
        interaction.ToTable(nameof(ManagedLoginInteraction), table => table.HasCheckConstraint(PairConstraint, dialect.Identifiers(PairSql)));
        var reference = builder.Entity<ManagedScopeReference>();
        reference.HasKey(value => new { value.SelectorDigest, value.ScopeId });
        FactorSchema.Text(reference, nameof(ManagedScopeReference.SelectorDigest), FactorSchema.DigestLength, dialect);
        FactorSchema.Text(reference, nameof(ManagedScopeReference.AudienceId), CatalogueRules.IdentifierLength, dialect);
        reference.HasIndex(value => value.ScopeId);
        reference.HasIndex(value => value.AudienceId);
        reference.HasOne<ManagedLoginInteraction>().WithMany().HasForeignKey(value => value.SelectorDigest).OnDelete(DeleteBehavior.Restrict);
        reference.HasOne<OpenIddictEntityFrameworkCoreScope>().WithMany().HasForeignKey(value => value.ScopeId).OnDelete(DeleteBehavior.Restrict);
        reference.HasOne<ApiAudience>().WithMany().HasForeignKey(value => value.AudienceId).OnDelete(DeleteBehavior.Restrict);
        reference.ToTable(nameof(ManagedScopeReference));
    }
}
