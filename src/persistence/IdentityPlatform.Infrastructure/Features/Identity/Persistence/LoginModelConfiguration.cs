using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Infrastructure.Features.Identity.Persistence;

public static class LoginModelConfiguration
{
    public static void Configure(ModelBuilder builder, bool sqlServer)
    {
        builder.Entity<PlatformUser>().Property(user => user.IsEnabled).HasDefaultValue(true);
        var manifest = builder.Entity<PilotManifest>();
        manifest.Property(value => value.Id).HasMaxLength(32);
        manifest.Property(value => value.UserId).HasMaxLength(450);
        manifest.Property(value => value.ApplicationId).HasMaxLength(100);
        manifest.Property(value => value.ScopeId).HasMaxLength(100);
        manifest.Property(value => value.UserName).HasMaxLength(128);
        manifest.Property(value => value.RedirectUri).HasMaxLength(512);

        var session = builder.Entity<LocalIdentitySession>();
        session.Property(value => value.Id).HasMaxLength(43);
        session.Property(value => value.UserId).HasMaxLength(450);
        session.Property(value => value.SecurityStamp).HasMaxLength(128);
        session.HasIndex(value => value.UserId);
        session.HasIndex(value => value.ExpiresAt);

        var interaction = builder.Entity<LoginInteraction>();
        interaction.Property(value => value.Id).HasMaxLength(43);
        interaction.Property(value => value.RequestTokenId).HasMaxLength(100);
        interaction.Property(value => value.RequestUri).HasMaxLength(512);
        interaction.Property(value => value.ClientId).HasMaxLength(100);
        interaction.Property(value => value.BrowserDigest).HasMaxLength(64);
        interaction.Property(value => value.CompletedSessionId).HasMaxLength(43);
        interaction.Property(value => value.CompletedUserId).HasMaxLength(450);
        interaction.HasIndex(value => value.RequestTokenId).IsUnique();
        interaction.HasIndex(value => value.ExpiresAt);

        var audit = builder.Entity<SecurityAuditIntent>();
        audit.Property(value => value.Event).HasMaxLength(64);
        audit.Property(value => value.ActorType).HasMaxLength(32);
        audit.Property(value => value.ActorId).HasMaxLength(450);
        audit.Property(value => value.TargetType).HasMaxLength(32);
        audit.Property(value => value.TargetId).HasMaxLength(450);
        audit.Property(value => value.Outcome).HasMaxLength(32);
        audit.HasIndex(value => value.OccurredAt);

        if (sqlServer)
        {
            // No cascades: removal of a native account must not erase audit or revive stale sessions.
            foreach (var type in new[] { typeof(PilotManifest), typeof(LocalIdentitySession), typeof(LoginInteraction) })
            {
                foreach (var property in builder.Entity(type).Metadata.GetProperties().Where(value => value.ClrType == typeof(string)))
                {
                    property.SetCollation("Latin1_General_100_BIN2");
                }
            }
        }
    }
}
