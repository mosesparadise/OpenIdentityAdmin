using IdentityPlatform.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;

/// <summary>Adds bounded protocol coordination without changing native tables, registration or existing migration history.</summary>
internal static class AdministratorProtocolModel
{
    private const int NativeIdLength = 450;
    private const int RedirectLength = 2048;
    private const int DigestLength = 64;
    private const string ProfileTable = "AdministratorProtocolProfile";
    private const string AssertionTable = "AdministratorClientAssertionUse";
    private const string StateTable = "AdministratorClientStateBudget";
    private const string ProfileRule = "CK_AdministratorProtocolProfile_State";
    private const string AssertionRule = "CK_AdministratorClientAssertionUse_Lifetime";
    private const string StateRule = "CK_AdministratorClientStateBudget_State";
    private const string ProfileState = "\"Id\" = 1 AND \"Revision\" > 0 AND \"LockRevision\" >= 0 AND \"ObservedAt\" >= 0";
    private const string AssertionLifetime = "\"CreatedAt\" >= 0 AND \"ExpiresAt\" > \"CreatedAt\" AND \"RetainUntil\" = \"ExpiresAt\" + 30";
    private const string BudgetState = "\"Id\" = 1 AND \"Revision\" >= 0";

    /// <summary>Requires explicit initialization for registration and replay ownership; migrations create no enabled profile.</summary>
    internal static void ConfigurePlatform(ModelBuilder builder, IDatabaseModelDialect dialect)
    {
        var profile = builder.Entity<AdministratorProtocolProfile>();
        profile.Property(row => row.Id).ValueGeneratedNever();
        profile.Property(row => row.Revision).IsConcurrencyToken();
        Text(profile.Property(row => row.ApplicationId), NativeIdLength, dialect);
        Text(profile.Property(row => row.ScopeId), NativeIdLength, dialect);
        Text(profile.Property(row => row.RedirectUri), RedirectLength, dialect);
        Text(profile.Property(row => row.PublicKeyFingerprint), DigestLength, dialect);
        profile.ToTable(ProfileTable, table => table.HasCheckConstraint(ProfileRule, dialect.Identifiers(ProfileState)));
        var assertion = builder.Entity<AdministratorClientAssertionUse>();
        assertion.HasKey(row => row.Digest);
        Text(assertion.Property(row => row.Digest), DigestLength, dialect);
        assertion.HasIndex(row => row.RetainUntil);
        assertion.ToTable(AssertionTable, table => table.HasCheckConstraint(AssertionRule, dialect.Identifiers(AssertionLifetime)));
    }

    /// <summary>Seeds only inert quota coordination; native state and product tickets remain manager-owned and separate.</summary>
    internal static void ConfigureSessions(ModelBuilder builder, IDatabaseModelDialect dialect)
    {
        var budget = builder.Entity<AdministratorClientStateBudget>();
        budget.Property(row => row.Id).ValueGeneratedNever();
        budget.HasData(new AdministratorClientStateBudget());
        budget.ToTable(StateTable, table => table.HasCheckConstraint(StateRule, dialect.Identifiers(BudgetState)));
    }

    /// <summary>Uses the registered provider's ordinal text semantics for protocol identities on every supported provider.</summary>
    private static void Text(PropertyBuilder<string> property, int length, IDatabaseModelDialect dialect) =>
        property.HasMaxLength(length).IsRequired().UseCollation(dialect.SessionOrdinalCollation);
}
