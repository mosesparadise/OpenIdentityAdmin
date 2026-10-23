using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IdentityPlatform.Migrations.SqlServer.Platform
{
    /// <inheritdoc />
    public partial class AdministratorIssuerContinuation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BrowserIntentDigest",
                schema: "platform",
                table: "AdministratorInteraction",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true,
                collation: "Latin1_General_100_BIN2");

            migrationBuilder.CreateTable(
                name: "AdministratorIssuerContinuation",
                schema: "platform",
                columns: table => new
                {
                    IntentDigest = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false, collation: "Latin1_General_100_BIN2"),
                    BrowserDigest = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false, collation: "Latin1_General_100_BIN2"),
                    ClientId = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false, collation: "Latin1_General_100_BIN2"),
                    State = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false, collation: "Latin1_General_100_BIN2"),
                    CreatedAt = table.Column<long>(type: "bigint", nullable: false),
                    ExpiresAt = table.Column<long>(type: "bigint", nullable: false),
                    ObservedAt = table.Column<long>(type: "bigint", nullable: false),
                    RetainUntil = table.Column<long>(type: "bigint", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    CorrelationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InteractionId = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true, collation: "Latin1_General_100_BIN2"),
                    InteractionBrowserDigest = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true, collation: "Latin1_General_100_BIN2"),
                    BrowserBudgetId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true, collation: "Latin1_General_100_BIN2"),
                    UserId = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true, collation: "Latin1_General_100_BIN2"),
                    SessionId = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true, collation: "Latin1_General_100_BIN2"),
                    SessionExpiresAt = table.Column<long>(type: "bigint", nullable: true),
                    CompletionDigest = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true, collation: "Latin1_General_100_BIN2"),
                    RequestId = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true, collation: "Latin1_General_100_BIN2")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdministratorIssuerContinuation", x => x.IntentDigest);
                    table.CheckConstraint("CK_AdministratorIssuerContinuation_Revision", "[Revision] > 0");
                    table.CheckConstraint("CK_AdministratorIssuerContinuation_Time", "[CreatedAt] > 0 AND [ExpiresAt] > [CreatedAt] AND [RetainUntil] >= [ExpiresAt]");
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdministratorIssuerContinuation_BrowserDigest",
                schema: "platform",
                table: "AdministratorIssuerContinuation",
                column: "BrowserDigest");

            migrationBuilder.CreateIndex(
                name: "IX_AdministratorIssuerContinuation_CompletionDigest",
                schema: "platform",
                table: "AdministratorIssuerContinuation",
                column: "CompletionDigest",
                unique: true,
                filter: "[CompletionDigest] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AdministratorIssuerContinuation_InteractionId",
                schema: "platform",
                table: "AdministratorIssuerContinuation",
                column: "InteractionId",
                unique: true,
                filter: "[InteractionId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AdministratorIssuerContinuation_RequestId",
                schema: "platform",
                table: "AdministratorIssuerContinuation",
                column: "RequestId",
                unique: true,
                filter: "[RequestId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AdministratorIssuerContinuation_RetainUntil",
                schema: "platform",
                table: "AdministratorIssuerContinuation",
                column: "RetainUntil");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdministratorIssuerContinuation",
                schema: "platform");

            migrationBuilder.DropColumn(
                name: "BrowserIntentDigest",
                schema: "platform",
                table: "AdministratorInteraction");
        }
    }
}
