using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IdentityPlatform.Migrations.SqlServer.Platform
{
    /// <inheritdoc />
    public partial class ApplicationLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Enabled",
                schema: "platform",
                table: "ApplicationPolicy",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "ApplicationLifecycleAuditDetail",
                schema: "platform",
                columns: table => new
                {
                    AuditId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReasonCode = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationLifecycleAuditDetail", x => x.AuditId);
                    table.CheckConstraint("CK_ApplicationLifecycleAuditDetail_Reason", "[ReasonCode] IN (1, 2, 3)");
                    table.ForeignKey(
                        name: "FK_ApplicationLifecycleAuditDetail_SecurityAuditIntent_AuditId",
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
            migrationBuilder.DropTable(
                name: "ApplicationLifecycleAuditDetail",
                schema: "platform");

            migrationBuilder.DropColumn(
                name: "Enabled",
                schema: "platform",
                table: "ApplicationPolicy");
        }
    }
}
