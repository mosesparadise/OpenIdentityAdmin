using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IdentityPlatform.Migrations.PostgreSql.Platform
{
    /// <inheritdoc />
    public partial class ManagedKeyAuditRevision : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApplicationKeyAuditDetail",
                schema: "platform",
                columns: table => new
                {
                    AuditId = table.Column<Guid>(type: "uuid", nullable: false),
                    PreviousRevision = table.Column<long>(type: "bigint", nullable: false),
                    CredentialRevision = table.Column<long>(type: "bigint", nullable: false),
                    Fingerprint = table.Column<string>(type: "character varying(64)", unicode: false, maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationKeyAuditDetail", x => x.AuditId);
                    table.CheckConstraint("CK_ApplicationKeyAuditDetail_Revision", "\"PreviousRevision\" >= 0 AND \"CredentialRevision\" > \"PreviousRevision\" AND \"CredentialRevision\" - \"PreviousRevision\" = 1");
                    table.ForeignKey(
                        name: "FK_ApplicationKeyAuditDetail_SecurityAuditIntent_AuditId",
                        column: x => x.AuditId,
                        principalSchema: "platform",
                        principalTable: "SecurityAuditIntent",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("LOCK TABLE \"platform\".\"ApplicationKeyAuditDetail\" IN ACCESS EXCLUSIVE MODE; DO $$ BEGIN IF EXISTS (SELECT 1 FROM \"platform\".\"ApplicationKeyAuditDetail\") THEN RAISE EXCEPTION 'Client key audit history must be retained'; END IF; END $$;");

            migrationBuilder.DropTable(
                name: "ApplicationKeyAuditDetail",
                schema: "platform");
        }
    }
}
