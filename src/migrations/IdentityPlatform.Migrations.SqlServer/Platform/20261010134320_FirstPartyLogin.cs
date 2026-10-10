using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IdentityPlatform.Migrations.SqlServer.Platform
{
    /// <inheritdoc />
    public partial class FirstPartyLogin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsEnabled",
                schema: "platform",
                table: "AspNetUsers",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "LocalIdentitySession",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(43)", maxLength: 43, nullable: false, collation: "Latin1_General_100_BIN2"),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false, collation: "Latin1_General_100_BIN2"),
                    SecurityStamp = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false, collation: "Latin1_General_100_BIN2"),
                    AuthenticatedAt = table.Column<long>(type: "bigint", nullable: false),
                    ExpiresAt = table.Column<long>(type: "bigint", nullable: false),
                    Revoked = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LocalIdentitySession", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LoginInteraction",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(43)", maxLength: 43, nullable: false, collation: "Latin1_General_100_BIN2"),
                    RequestTokenId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, collation: "Latin1_General_100_BIN2"),
                    RequestUri = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false, collation: "Latin1_General_100_BIN2"),
                    ClientId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, collation: "Latin1_General_100_BIN2"),
                    BrowserDigest = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false, collation: "Latin1_General_100_BIN2"),
                    CreatedAt = table.Column<long>(type: "bigint", nullable: false),
                    ExpiresAt = table.Column<long>(type: "bigint", nullable: false),
                    CompletedSessionId = table.Column<string>(type: "nvarchar(43)", maxLength: 43, nullable: true, collation: "Latin1_General_100_BIN2"),
                    CompletedUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true, collation: "Latin1_General_100_BIN2"),
                    Consumed = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoginInteraction", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PilotManifest",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false, collation: "Latin1_General_100_BIN2"),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false, collation: "Latin1_General_100_BIN2"),
                    ApplicationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, collation: "Latin1_General_100_BIN2"),
                    ScopeId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, collation: "Latin1_General_100_BIN2"),
                    UserName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false, collation: "Latin1_General_100_BIN2"),
                    RedirectUri = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false, collation: "Latin1_General_100_BIN2")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PilotManifest", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SecurityAuditIntent",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Event = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ActorType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ActorId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    TargetType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    TargetId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    Outcome = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CorrelationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OccurredAt = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SecurityAuditIntent", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LocalIdentitySession_ExpiresAt",
                schema: "platform",
                table: "LocalIdentitySession",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_LocalIdentitySession_UserId",
                schema: "platform",
                table: "LocalIdentitySession",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_LoginInteraction_ExpiresAt",
                schema: "platform",
                table: "LoginInteraction",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_LoginInteraction_RequestTokenId",
                schema: "platform",
                table: "LoginInteraction",
                column: "RequestTokenId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SecurityAuditIntent_OccurredAt",
                schema: "platform",
                table: "SecurityAuditIntent",
                column: "OccurredAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LocalIdentitySession",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "LoginInteraction",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "PilotManifest",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "SecurityAuditIntent",
                schema: "platform");

            migrationBuilder.DropColumn(
                name: "IsEnabled",
                schema: "platform",
                table: "AspNetUsers");
        }
    }
}
