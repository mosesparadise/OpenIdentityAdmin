using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Infrastructure.Features.Identity.Persistence;

/// <summary>Maps inert administrator state with identical bounds and restrictive ownership on both providers.</summary>
internal static class FactorModelConfiguration
{
    /// <summary>Registers all factor, interaction, session and admission schema components.</summary>
    internal static void Configure(ModelBuilder builder, bool sqlServer)
    {
        ConfigureAuthentication(builder, sqlServer);
        ConfigureAdmission(builder, sqlServer);
        ConfigureBrowserBudget(builder, sqlServer);
        ConfigureInteraction(builder, sqlServer);
        ConfigureAuthorization(builder, sqlServer);
        ConfigureFactor(builder, sqlServer);
        ConfigureReplay(builder, sqlServer);
        ConfigureRecoveryCode(builder, sqlServer);
        ConfigureSession(builder, sqlServer);
        ConfigureEnrollment(builder, sqlServer);
    }

    /// <summary>Configures bounded authentication records without granting runtime authority.</summary>
    private static void ConfigureAuthentication(ModelBuilder builder, bool sqlServer)
    {
        var entity = builder.Entity<AdministratorAuthentication>();
        entity.HasKey(value => value.UserId);
        FactorSchema.Text(entity, nameof(AdministratorAuthentication.UserId), FactorSchema.UserLength, sqlServer);
        entity.HasOne<AdministratorEnrollment>().WithOne().HasForeignKey<AdministratorAuthentication>(value => value.UserId).OnDelete(DeleteBehavior.Restrict);
        const string Valid = "\"Generation\" > 0 AND \"Revision\" >= 0 AND \"Failures\" >= 0 AND \"Failures\" <= 5";
        const string Rule = "State";
        FactorSchema.Check(entity, Rule, Valid, sqlServer);
        const string AllocatorRule = "FactorAllocator";
        const string AllocatorValid = "\"LastAllocatedFactorGeneration\" >= 0";
        FactorSchema.Check(entity, AllocatorRule, AllocatorValid, sqlServer);
    }

    /// <summary>Configures bounded admission records without granting runtime authority.</summary>
    private static void ConfigureAdmission(ModelBuilder builder, bool sqlServer)
    {
        var entity = builder.Entity<AdministratorAdmission>();
        entity.Property(value => value.Id).ValueGeneratedNever();
        entity.HasData(new AdministratorAdmission());
        const string Valid = "\"Id\" = 1 AND \"Revision\" >= 0";
        const string Rule = "Singleton";
        FactorSchema.Check(entity, Rule, Valid, sqlServer);
    }

    /// <summary>Configures bounded browserbudget records without granting runtime authority.</summary>
    private static void ConfigureBrowserBudget(ModelBuilder builder, bool sqlServer)
    {
        var entity = builder.Entity<AdministratorBrowserBudget>();
        FactorSchema.Text(entity, nameof(AdministratorBrowserBudget.Id), FactorSchema.DigestLength, sqlServer);
        entity.HasIndex(value => value.ExpiresAt);
        const string Valid = "\"Revision\" >= 0 AND \"ExpiresAt\" > 0";
        const string Rule = "State";
        FactorSchema.Check(entity, Rule, Valid, sqlServer);
    }

    /// <summary>Configures bounded interaction records without granting runtime authority.</summary>
    private static void ConfigureInteraction(ModelBuilder builder, bool sqlServer)
    {
        var entity = FactorSchema.Material<AdministratorInteraction>(builder, sqlServer);
        FactorSchema.Text(entity, nameof(AdministratorInteraction.PublicId), FactorSchema.DigestLength, sqlServer);
        FactorSchema.Text(entity, nameof(AdministratorInteraction.BrowserBudgetId), FactorSchema.DigestLength, sqlServer);
        FactorSchema.Text(entity, nameof(AdministratorInteraction.BrowserDigest), FactorSchema.DigestLength, sqlServer);
        FactorSchema.Text(entity, nameof(AdministratorInteraction.Purpose), FactorSchema.LabelLength, sqlServer);
        FactorSchema.Text(entity, nameof(AdministratorInteraction.State), FactorSchema.LabelLength, sqlServer);
        FactorSchema.Text(entity, nameof(AdministratorInteraction.SecurityStamp), FactorSchema.StampLength, sqlServer);
        FactorSchema.Text(entity, nameof(AdministratorInteraction.StagedSeed), FactorSchema.CiphertextLength, sqlServer);
        entity.HasOne<AdministratorBrowserBudget>().WithMany().HasForeignKey(value => value.BrowserBudgetId).OnDelete(DeleteBehavior.Restrict);
        entity.HasIndex(value => value.PublicId).IsUnique();
        entity.HasIndex(value => new { value.UserId, value.Generation });
        entity.HasIndex(value => value.ExpiresAt);
        const string Valid = "\"Generation\" > 0 AND \"DestinationGeneration\" > 0 AND \"Revision\" >= 0 AND \"Attempts\" >= 0 AND \"Attempts\" <= 5 AND \"CreatedAt\" >= 0 AND \"ExpiresAt\" > \"CreatedAt\"";
        const string Rule = "State";
        FactorSchema.Check(entity, Rule, Valid, sqlServer);
        const string EnrollmentRule = "EnrollmentRevision";
        const string EnrollmentValid = "\"ExpectedEnrollmentRevision\" IS NULL OR \"ExpectedEnrollmentRevision\" >= 0";
        FactorSchema.Check(entity, EnrollmentRule, EnrollmentValid, sqlServer);
    }

    /// <summary>Bounds additive step-up bindings; legacy rows retain no replacement authorization.</summary>
    private static void ConfigureAuthorization(ModelBuilder builder, bool sqlServer)
    {
        var entity = builder.Entity<AdministratorInteraction>();
        FactorSchema.Text(entity, nameof(AdministratorInteraction.AuthorizationKind), FactorSchema.LabelLength, sqlServer);
        FactorSchema.Text(entity, nameof(AdministratorInteraction.SessionPublicId), FactorSchema.DigestLength, sqlServer);
        const string Rule = "Authorization";
        const string Valid = "(\"AuthorizationKind\" IS NULL AND \"AuthorizationVerifiedAt\" IS NULL AND \"SourceFactorGeneration\" IS NULL) OR " +
            "(\"AuthorizationKind\" IS NOT NULL AND \"AuthorizationVerifiedAt\" IS NOT NULL AND \"SourceFactorGeneration\" IS NOT NULL AND " +
            "\"AuthorizationVerifiedAt\" >= \"PasswordVerifiedAt\" AND \"SourceFactorGeneration\" > 0)";
        FactorSchema.Check(entity, Rule, Valid, sqlServer);
    }

    /// <summary>Configures bounded factor records without granting runtime authority.</summary>
    private static void ConfigureFactor(ModelBuilder builder, bool sqlServer)
    {
        var entity = FactorSchema.Material<AdministratorFactor>(builder, sqlServer);
        FactorSchema.Text(entity, nameof(AdministratorFactor.ProtectedSeed), FactorSchema.CiphertextLength, sqlServer);
        entity.HasIndex(value => new { value.UserId, value.Generation }).IsUnique();
        const string Valid = "\"Generation\" > 0";
        const string Rule = "Generation";
        FactorSchema.Check(entity, Rule, Valid, sqlServer);
    }

    /// <summary>Configures bounded replay records without granting runtime authority.</summary>
    private static void ConfigureReplay(ModelBuilder builder, bool sqlServer)
    {
        var entity = FactorSchema.Material<AdministratorReplay>(builder, sqlServer);
        FactorSchema.Text(entity, nameof(AdministratorReplay.ProtectedCode), FactorSchema.CiphertextLength, sqlServer);
        entity.HasIndex(value => new { value.UserId, value.Generation });
        entity.HasIndex(value => value.ExpiresAt);
        const string Valid = "\"Generation\" > 0 AND \"AcceptedAt\" >= 0 AND \"ExpiresAt\" > \"AcceptedAt\"";
        const string Rule = "State";
        FactorSchema.Check(entity, Rule, Valid, sqlServer);
    }

    /// <summary>Configures bounded recoverycode records without granting runtime authority.</summary>
    private static void ConfigureRecoveryCode(ModelBuilder builder, bool sqlServer)
    {
        var entity = FactorSchema.Material<AdministratorRecoveryCode>(builder, sqlServer);
        FactorSchema.Text(entity, nameof(AdministratorRecoveryCode.Verifier), FactorSchema.DigestLength, sqlServer);
        entity.HasIndex(value => new { value.UserId, value.Generation, value.Verifier }).IsUnique();
        const string Valid = "\"Generation\" > 0 AND (\"RedeemedAt\" IS NULL OR \"RedeemedAt\" >= 0)";
        const string Rule = "State";
        FactorSchema.Check(entity, Rule, Valid, sqlServer);
    }

    /// <summary>Configures bounded session records without granting runtime authority.</summary>
    private static void ConfigureSession(ModelBuilder builder, bool sqlServer)
    {
        var entity = FactorSchema.Material<AdministratorSession>(builder, sqlServer);
        FactorSchema.Text(entity, nameof(AdministratorSession.PublicId), FactorSchema.DigestLength, sqlServer);
        FactorSchema.Text(entity, nameof(AdministratorSession.AuthenticationMethods), FactorSchema.LabelLength, sqlServer);
        FactorSchema.Text(entity, nameof(AdministratorSession.SecurityStamp), FactorSchema.StampLength, sqlServer);
        entity.HasIndex(value => value.PublicId).IsUnique();
        entity.HasIndex(value => new { value.UserId, value.Generation });
        entity.HasIndex(value => value.ExpiresAt);
        const string Valid = "\"Generation\" > 0 AND \"FactorGeneration\" > 0 AND \"Revision\" >= 0 AND \"CreatedAt\" >= 0 AND \"LastActivityAt\" >= \"CreatedAt\" AND \"ExpiresAt\" > \"LastActivityAt\" AND \"AuthenticationMethods\" = 'pwd otp' AND \"PasswordVerifiedAt\" >= 0 AND \"FactorVerifiedAt\" >= 0 AND \"PasswordVerifiedAt\" <= \"FactorVerifiedAt\" AND \"FactorVerifiedAt\" <= \"CreatedAt\"";
        const string Rule = "State";
        FactorSchema.Check(entity, Rule, Valid, sqlServer);
        const string MethodLengthRule = "MethodLength";
        const string SqlMethodLength = "DATALENGTH([AuthenticationMethods]) = 14";
        const string PostgresMethodLength = "octet_length(\"AuthenticationMethods\") = 7";
        FactorSchema.Check(entity, MethodLengthRule, sqlServer ? SqlMethodLength : PostgresMethodLength, sqlServer);
    }

    /// <summary>Configures bounded enrollment records without granting runtime authority.</summary>
    private static void ConfigureEnrollment(ModelBuilder builder, bool sqlServer)
    {
        var entity = builder.Entity<AdministratorEnrollment>();
        const string Valid = "\"FactorGeneration\" >= 0 AND \"RecoveryGeneration\" >= 0";
        const string Rule = "Generations";
        FactorSchema.Check(entity, Rule, Valid, sqlServer);
    }
}
