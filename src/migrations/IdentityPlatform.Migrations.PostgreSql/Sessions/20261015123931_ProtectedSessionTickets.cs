using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IdentityPlatform.Migrations.PostgreSql.Sessions
{
    /// <inheritdoc />
    public partial class ProtectedSessionTickets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AdminSessionAudit",
                schema: "bff",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CorrelationId = table.Column<Guid>(type: "uuid", nullable: false),
                    OccurredAt = table.Column<long>(type: "bigint", nullable: false),
                    Operation = table.Column<int>(type: "integer", nullable: false),
                    Outcome = table.Column<int>(type: "integer", nullable: false),
                    Digest = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false, collation: "C"),
                    Revision = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminSessionAudit", x => x.Id);
                    table.CheckConstraint("CK_AdminSessionAudit_Digest", "\"Digest\" ~ '^[0-9A-F]{64}$'");
                    table.CheckConstraint("CK_AdminSessionAudit_State", "\"Revision\" > 0 AND \"OccurredAt\" >= 0 AND \"Operation\" IN (1, 2, 3) AND \"Outcome\" = 1");
                });

            migrationBuilder.CreateTable(
                name: "AdminSessionTicket",
                schema: "bff",
                columns: table => new
                {
                    Digest = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false, collation: "C"),
                    Subject = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false, collation: "C"),
                    SourceSession = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false, collation: "C"),
                    ProtectedTicket = table.Column<byte[]>(type: "bytea", maxLength: 98304, nullable: false),
                    CreatedAt = table.Column<long>(type: "bigint", nullable: false),
                    LastActivityAt = table.Column<long>(type: "bigint", nullable: false),
                    AbsoluteExpiresAt = table.Column<long>(type: "bigint", nullable: false),
                    IdleLifetime = table.Column<long>(type: "bigint", nullable: false),
                    IdleExpiresAt = table.Column<long>(type: "bigint", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    RevokedAt = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminSessionTicket", x => x.Digest);
                    table.CheckConstraint("CK_AdminSessionTicket_Binding", "\"Digest\" ~ '^[0-9A-F]{64}$' AND octet_length(\"Subject\") > 0 AND octet_length(\"SourceSession\") > 0");
                    table.CheckConstraint("CK_AdminSessionTicket_Payload", "octet_length(\"ProtectedTicket\") > 0 AND octet_length(\"ProtectedTicket\") <= 98304");
                    table.CheckConstraint("CK_AdminSessionTicket_State", "\"Revision\" > 0 AND \"CreatedAt\" >= 0 AND \"LastActivityAt\" >= \"CreatedAt\" AND \"AbsoluteExpiresAt\" > \"LastActivityAt\" AND \"IdleExpiresAt\" > \"LastActivityAt\" AND \"IdleExpiresAt\" <= \"AbsoluteExpiresAt\" AND \"IdleLifetime\" > 0 AND \"IdleLifetime\" <= 18000000000");
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdminSessionAudit_CorrelationId",
                schema: "bff",
                table: "AdminSessionAudit",
                column: "CorrelationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AdminSessionAudit_Digest_OccurredAt",
                schema: "bff",
                table: "AdminSessionAudit",
                columns: new[] { "Digest", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AdminSessionTicket_AbsoluteExpiresAt",
                schema: "bff",
                table: "AdminSessionTicket",
                column: "AbsoluteExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_AdminSessionTicket_IdleExpiresAt",
                schema: "bff",
                table: "AdminSessionTicket",
                column: "IdleExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_AdminSessionTicket_RevokedAt",
                schema: "bff",
                table: "AdminSessionTicket",
                column: "RevokedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AdminSessionTicket_Subject_SourceSession",
                schema: "bff",
                table: "AdminSessionTicket",
                columns: new[] { "Subject", "SourceSession" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdminSessionAudit",
                schema: "bff");

            migrationBuilder.DropTable(
                name: "AdminSessionTicket",
                schema: "bff");
        }
    }
}
