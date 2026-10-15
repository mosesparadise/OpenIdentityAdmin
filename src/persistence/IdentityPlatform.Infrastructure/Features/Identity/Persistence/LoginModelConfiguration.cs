using IdentityPlatform.Database;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Infrastructure.Features.Identity.Persistence;

/// <summary>Maps existing first-party login state with bounded fields and provider-owned ordinal semantics.</summary>
/// <example><code>LoginModelConfiguration.Configure(builder, provider.Model);</code></example>
public static class LoginModelConfiguration
{
    private const int LabelLength = 32;
    private const int UserIdLength = 450;
    private const int NativeIdLength = 100;
    private const int UserNameLength = 128;
    private const int RedirectLength = 512;
    private const int PublicIdLength = 43;
    private const int DigestLength = 64;

    /// <summary>Composes unchanged native-user, login, audit and comparison mappings.</summary>
    /// <example><code>LoginModelConfiguration.Configure(builder, provider.Model);</code></example>
    public static void Configure(ModelBuilder builder, IDatabaseModelDialect dialect)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(dialect);
        builder.Entity<PlatformUser>().Property(user => user.IsEnabled).HasDefaultValue(true);
        ConfigureManifest(builder);
        ConfigureSession(builder);
        ConfigureBudget(builder);
        ConfigureInteraction(builder);
        ConfigureAudit(builder);
        ConfigureOrdinal(builder, dialect);
    }

    /// <summary>Preserves the existing manifest mapping and storage boundaries.</summary>
    private static void ConfigureManifest(ModelBuilder builder)
    {
        var manifest = builder.Entity<PilotManifest>();
        manifest.Property(value => value.Id).HasMaxLength(LabelLength);
        manifest.Property(value => value.UserId).HasMaxLength(UserIdLength);
        manifest.Property(value => value.ApplicationId).HasMaxLength(NativeIdLength);
        manifest.Property(value => value.ScopeId).HasMaxLength(NativeIdLength);
        manifest.Property(value => value.UserName).HasMaxLength(UserNameLength);
        manifest.Property(value => value.RedirectUri).HasMaxLength(RedirectLength);
    }

    /// <summary>Preserves the existing session mapping and storage boundaries.</summary>
    private static void ConfigureSession(ModelBuilder builder)
    {
        var session = builder.Entity<LocalIdentitySession>();
        session.Property(value => value.Id).HasMaxLength(PublicIdLength);
        session.Property(value => value.UserId).HasMaxLength(UserIdLength);
        session.Property(value => value.SecurityStamp).HasMaxLength(UserNameLength);
        session.HasIndex(value => value.UserId);
        session.HasIndex(value => value.ExpiresAt);
    }

    /// <summary>Preserves the existing budget mapping and storage boundaries.</summary>
    private static void ConfigureBudget(ModelBuilder builder)
    {
        var budget = builder.Entity<LoginBrowserBudget>();
        budget.Property(value => value.Id).HasMaxLength(DigestLength);
        budget.HasIndex(value => value.ExpiresAt);
    }

    /// <summary>Preserves the existing interaction mapping and storage boundaries.</summary>
    private static void ConfigureInteraction(ModelBuilder builder)
    {
        var interaction = builder.Entity<LoginInteraction>();
        interaction.Property(value => value.Id).HasMaxLength(PublicIdLength);
        interaction.Property(value => value.RequestTokenId).HasMaxLength(NativeIdLength);
        interaction.Property(value => value.RequestUri).HasMaxLength(RedirectLength);
        interaction.Property(value => value.ClientId).HasMaxLength(NativeIdLength);
        interaction.Property(value => value.BrowserDigest).HasMaxLength(DigestLength);
        interaction.Property(value => value.BudgetId).HasMaxLength(DigestLength);
        interaction.HasOne<LoginBrowserBudget>().WithMany().HasForeignKey(value => value.BudgetId).OnDelete(DeleteBehavior.Restrict);
        interaction.HasIndex(value => new { value.BudgetId, value.Consumed, value.ExpiresAt });
        interaction.Property(value => value.CompletedSessionId).HasMaxLength(PublicIdLength);
        interaction.Property(value => value.CompletedUserId).HasMaxLength(UserIdLength);
        interaction.HasIndex(value => value.RequestTokenId).IsUnique();
        interaction.HasIndex(value => value.ExpiresAt);
    }

    /// <summary>Preserves the existing audit mapping and storage boundaries.</summary>
    private static void ConfigureAudit(ModelBuilder builder)
    {
        var audit = builder.Entity<SecurityAuditIntent>();
        audit.Property(value => value.Event).HasMaxLength(DigestLength);
        audit.Property(value => value.ActorType).HasMaxLength(LabelLength);
        audit.Property(value => value.ActorId).HasMaxLength(UserIdLength);
        audit.Property(value => value.TargetType).HasMaxLength(LabelLength);
        audit.Property(value => value.TargetId).HasMaxLength(UserIdLength);
        audit.Property(value => value.Outcome).HasMaxLength(LabelLength);
        audit.HasIndex(value => value.OccurredAt);
    }

    /// <summary>Preserves the existing ordinal mapping and storage boundaries.</summary>
    private static void ConfigureOrdinal(ModelBuilder builder, IDatabaseModelDialect dialect)
    {
        if (dialect.IdentityOrdinalCollation is { } ordinal)
        {
            // No cascades: removal of a native account must not erase audit or revive stale sessions.
            foreach (var type in new[] { typeof(PilotManifest), typeof(LocalIdentitySession), typeof(LoginInteraction), typeof(LoginBrowserBudget) })
            {
                foreach (var property in builder.Entity(type).Metadata.GetProperties().Where(value => value.ClrType == typeof(string)))
                {
                    property.SetCollation(ordinal);
                }
            }
        }
    }
}
