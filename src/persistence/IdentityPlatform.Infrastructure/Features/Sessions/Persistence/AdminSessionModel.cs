using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IdentityPlatform.Infrastructure.Features.Sessions.Persistence;

/// <summary>Maps additive session tables with bounded payloads, ordinal identifiers and no audit cascade.</summary>
internal static class AdminSessionModel
{
    private const string SqlOrdinal = "Latin1_General_100_BIN2";
    private const string PostgresOrdinal = "C";
    private const string TicketTable = "AdminSessionTicket";
    private const string AuditTable = "AdminSessionAudit";
    private const string TicketStateRule = "CK_AdminSessionTicket_State";
    private const string AuditStateRule = "CK_AdminSessionAudit_State";
    private const string TicketBindingRule = "CK_AdminSessionTicket_Binding";
    private const string AuditDigestRule = "CK_AdminSessionAudit_Digest";
    private const string DigestSql = "DATALENGTH([Digest]) = 128 AND [Digest] NOT LIKE '%[^0-9A-F]%'";
    private const string DigestPostgres = "\"Digest\" ~ '^[0-9A-F]{64}$'";
    private const string BindingSql = " AND DATALENGTH([Subject]) > 0 AND DATALENGTH([SourceSession]) > 0";
    private const string BindingPostgres = " AND octet_length(\"Subject\") > 0 AND octet_length(\"SourceSession\") > 0";
    private const string PayloadRule = "CK_AdminSessionTicket_Payload";
    private const string PayloadSql = "DATALENGTH([ProtectedTicket]) > 0 AND DATALENGTH([ProtectedTicket]) <= 98304";
    private const string PayloadPostgres = "octet_length(\"ProtectedTicket\") > 0 AND octet_length(\"ProtectedTicket\") <= 98304";
    private const string TicketState = "\"Revision\" > 0 AND \"CreatedAt\" >= 0 AND \"LastActivityAt\" >= \"CreatedAt\" AND \"AbsoluteExpiresAt\" > \"LastActivityAt\" AND \"IdleExpiresAt\" > \"LastActivityAt\" AND \"IdleExpiresAt\" <= \"AbsoluteExpiresAt\" AND \"IdleLifetime\" > 0 AND \"IdleLifetime\" <= 18000000000";
    private const string AuditState = "\"Revision\" > 0 AND \"OccurredAt\" >= 0 AND \"Operation\" IN (1, 2, 3) AND \"Outcome\" = 1";

    /// <summary>Adds the ticket and independent audit mappings without changing native client-state tables.</summary>
    internal static void Configure(ModelBuilder builder, bool sqlServer)
    {
        var ticket = builder.Entity<AdminSessionTicket>();
        ticket.HasKey(row => row.Digest);
        Text(ticket.Property(row => row.Digest), SessionTicketLimits.DigestLength, sqlServer);
        Text(ticket.Property(row => row.Subject), SessionTicketLimits.BindingLength, sqlServer);
        Text(ticket.Property(row => row.SourceSession), SessionTicketLimits.BindingLength, sqlServer);
        ticket.Property(row => row.ProtectedTicket).HasMaxLength(SessionTicketLimits.ProtectedBytes).IsRequired();
        ticket.HasIndex(row => row.IdleExpiresAt);
        ticket.HasIndex(row => row.AbsoluteExpiresAt);
        ticket.HasIndex(row => row.RevokedAt);
        ticket.HasIndex(row => new { row.Subject, row.SourceSession });
        ticket.ToTable(TicketTable, table =>
        {
            table.HasCheckConstraint(TicketStateRule, TicketState);
            table.HasCheckConstraint(TicketBindingRule, sqlServer ? DigestSql + BindingSql : DigestPostgres + BindingPostgres);
            table.HasCheckConstraint(PayloadRule, sqlServer ? PayloadSql : PayloadPostgres);
        });
        ConfigureAudit(builder, sqlServer);
    }

    /// <summary>Retains audit rows after ticket deletion and bounds all correlated metadata.</summary>
    private static void ConfigureAudit(ModelBuilder builder, bool sqlServer)
    {
        var audit = builder.Entity<AdminSessionAudit>();
        audit.Property(row => row.Id).ValueGeneratedNever();
        audit.HasIndex(row => row.CorrelationId).IsUnique();
        audit.HasIndex(row => new { row.Digest, row.OccurredAt });
        Text(audit.Property(row => row.Digest), SessionTicketLimits.DigestLength, sqlServer);
        audit.ToTable(AuditTable, table =>
        {
            table.HasCheckConstraint(AuditStateRule, AuditState);
            table.HasCheckConstraint(AuditDigestRule, sqlServer ? DigestSql : DigestPostgres);
        });
    }

    /// <summary>Uses explicit ordinal collations for both providers rather than database-default protocol equality.</summary>
    private static void Text(PropertyBuilder<string> property, int length, bool sqlServer) =>
        property.HasMaxLength(length).IsRequired().UseCollation(sqlServer ? SqlOrdinal : PostgresOrdinal);
}
