using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IdentityPlatform.Migrations.PostgreSql.Platform
{
    /// <inheritdoc />
    public partial class ApplicationRetirement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Retired",
                schema: "platform",
                table: "ApplicationPolicy",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "ApplicationRetirement",
                schema: "platform",
                columns: table => new
                {
                    ApplicationId = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    ActorId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    OriginalRevision = table.Column<long>(type: "bigint", nullable: false),
                    RetiredRevision = table.Column<long>(type: "bigint", nullable: false),
                    Reason = table.Column<int>(type: "integer", nullable: false),
                    RetiredAt = table.Column<long>(type: "bigint", nullable: false),
                    RequestKey = table.Column<Guid>(type: "uuid", nullable: false),
                    ScopedKeyHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RequestHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ReplayUntil = table.Column<long>(type: "bigint", nullable: false),
                    AuditId = table.Column<Guid>(type: "uuid", nullable: false),
                    GrantJobId = table.Column<Guid>(type: "uuid", nullable: false),
                    GrantApprovalId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationRetirement", x => x.ApplicationId);
                    table.CheckConstraint("CK_ApplicationRetirement_Reason", "\"Reason\" IN (1, 2, 3)");
                    table.CheckConstraint("CK_ApplicationRetirement_Revision", "\"OriginalRevision\" > 0 AND \"RetiredRevision\" - \"OriginalRevision\" = 1");
                    table.CheckConstraint("CK_ApplicationRetirement_Time", "\"RetiredAt\" >= 0 AND \"ReplayUntil\" > \"RetiredAt\" AND \"ReplayUntil\" - \"RetiredAt\" = 86400");
                    table.ForeignKey(
                        name: "FK_ApplicationRetirement_ApplicationGrantApproval_GrantApprova~",
                        column: x => x.GrantApprovalId,
                        principalSchema: "platform",
                        principalTable: "ApplicationGrantApproval",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ApplicationRetirement_ApplicationGrantJob_GrantJobId",
                        column: x => x.GrantJobId,
                        principalSchema: "platform",
                        principalTable: "ApplicationGrantJob",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ApplicationRetirement_ApplicationPolicy_ApplicationId",
                        column: x => x.ApplicationId,
                        principalSchema: "platform",
                        principalTable: "ApplicationPolicy",
                        principalColumn: "ApplicationId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ApplicationRetirement_SecurityAuditIntent_AuditId",
                        column: x => x.AuditId,
                        principalSchema: "platform",
                        principalTable: "SecurityAuditIntent",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_ApplicationPolicy_Retirement",
                schema: "platform",
                table: "ApplicationPolicy",
                sql: "CAST(\"Retired\" AS INTEGER) + CAST(\"Enabled\" AS INTEGER) <= 1");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationRetirement_AuditId",
                schema: "platform",
                table: "ApplicationRetirement",
                column: "AuditId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationRetirement_GrantApprovalId",
                schema: "platform",
                table: "ApplicationRetirement",
                column: "GrantApprovalId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationRetirement_GrantJobId",
                schema: "platform",
                table: "ApplicationRetirement",
                column: "GrantJobId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationRetirement_ScopedKeyHash",
                schema: "platform",
                table: "ApplicationRetirement",
                column: "ScopedKeyHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApplicationRetirement",
                schema: "platform");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ApplicationPolicy_Retirement",
                schema: "platform",
                table: "ApplicationPolicy");

            migrationBuilder.DropColumn(
                name: "Retired",
                schema: "platform",
                table: "ApplicationPolicy");
        }
    }
}
