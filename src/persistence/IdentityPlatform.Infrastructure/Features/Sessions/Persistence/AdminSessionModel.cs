using IdentityPlatform.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IdentityPlatform.Infrastructure.Features.Sessions.Persistence;

/// <summary>Maps additive session tables with bounded payloads, ordinal identifiers and no audit cascade.</summary>
internal static class AdminSessionModel
{
    private const string TicketTable = "AdminSessionTicket";
    private const string AuditTable = "AdminSessionAudit";
    private const string TicketStateRule = "CK_AdminSessionTicket_State";
    private const string AuditStateRule = "CK_AdminSessionAudit_State";
    private const string TicketBindingRule = "CK_AdminSessionTicket_Binding";
    private const string AuditDigestRule = "CK_AdminSessionAudit_Digest";
    private const string PayloadRule = "CK_AdminSessionTicket_Payload";
    private const string TicketState = "\"Revision\" > 0 AND \"CreatedAt\" >= 0 AND \"LastActivityAt\" >= \"CreatedAt\" AND \"AbsoluteExpiresAt\" > \"LastActivityAt\" AND \"IdleExpiresAt\" > \"LastActivityAt\" AND \"IdleExpiresAt\" <= \"AbsoluteExpiresAt\" AND \"IdleLifetime\" > 0 AND \"IdleLifetime\" <= 18000000000";
    private const string AuditState = "\"Revision\" > 0 AND \"OccurredAt\" >= 0 AND \"Operation\" IN (1, 2, 3) AND \"Outcome\" = 1";

    /// <summary>Adds the ticket and independent audit mappings without changing native client-state tables.</summary>
    internal static void Configure(ModelBuilder builder, IDatabaseModelDialect dialect)
    {
        var ticket = builder.Entity<AdminSessionTicket>();
        ticket.HasKey(row => row.Digest);
        Text(ticket.Property(row => row.Digest), SessionTicketLimits.DigestLength, dialect);
        Text(ticket.Property(row => row.Subject), SessionTicketLimits.BindingLength, dialect);
        Text(ticket.Property(row => row.SourceSession), SessionTicketLimits.BindingLength, dialect);
        ticket.Property(row => row.ProtectedTicket).HasMaxLength(SessionTicketLimits.ProtectedBytes).IsRequired();
        ticket.HasIndex(row => row.IdleExpiresAt);
        ticket.HasIndex(row => row.AbsoluteExpiresAt);
        ticket.HasIndex(row => row.RevokedAt);
        ticket.HasIndex(row => new { row.Subject, row.SourceSession });
        ticket.ToTable(TicketTable, table =>
        {
            table.HasCheckConstraint(TicketStateRule, TicketState);
            table.HasCheckConstraint(TicketBindingRule, dialect.SessionBindingConstraint);
            table.HasCheckConstraint(PayloadRule, dialect.SessionPayloadConstraint);
        });
        ConfigureAudit(builder, dialect);
    }

    /// <summary>Retains audit rows after ticket deletion and bounds all correlated metadata.</summary>
    private static void ConfigureAudit(ModelBuilder builder, IDatabaseModelDialect dialect)
    {
        var audit = builder.Entity<AdminSessionAudit>();
        audit.Property(row => row.Id).ValueGeneratedNever();
        audit.HasIndex(row => row.CorrelationId).IsUnique();
        audit.HasIndex(row => new { row.Digest, row.OccurredAt });
        Text(audit.Property(row => row.Digest), SessionTicketLimits.DigestLength, dialect);
        audit.ToTable(AuditTable, table =>
        {
            table.HasCheckConstraint(AuditStateRule, AuditState);
            table.HasCheckConstraint(AuditDigestRule, dialect.SessionDigestConstraint);
        });
    }

    /// <summary>Uses explicit ordinal collations for both providers rather than database-default protocol equality.</summary>
    private static void Text(PropertyBuilder<string> property, int length, IDatabaseModelDialect dialect) =>
        property.HasMaxLength(length).IsRequired().UseCollation(dialect.SessionOrdinalCollation);
}
