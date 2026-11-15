using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IdentityPlatform.Migrations.SqlServer.Platform
{
    /// <inheritdoc />
    public partial class ApplicationCloning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApplicationCloneAuditDetail",
                schema: "platform",
                columns: table => new
                {
                    AuditId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceApplicationId = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false, collation: "Latin1_General_100_BIN2"),
                    SourceVersion = table.Column<string>(type: "nvarchar(86)", maxLength: 86, nullable: false, collation: "Latin1_General_100_BIN2")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationCloneAuditDetail", x => x.AuditId);
                    table.ForeignKey(
                        name: "FK_ApplicationCloneAuditDetail_SecurityAuditIntent_AuditId",
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
                name: "ApplicationCloneAuditDetail",
                schema: "platform");
        }
    }
}
