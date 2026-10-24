using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IdentityPlatform.Migrations.SqlServer.Sessions
{
    /// <inheritdoc />
    public partial class BrowserSessionCoordination : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BrowserContext",
                schema: "bff",
                columns: table => new
                {
                    Digest = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false, collation: "Latin1_General_100_BIN2"),
                    CorrelationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Generation = table.Column<long>(type: "bigint", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    ObservedAt = table.Column<long>(type: "bigint", nullable: false),
                    ExpiresAt = table.Column<long>(type: "bigint", nullable: false),
                    RetainUntil = table.Column<long>(type: "bigint", nullable: false),
                    TicketDigest = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true, collation: "Latin1_General_100_BIN2")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BrowserContext", x => x.Digest);
                    table.CheckConstraint("CK_BrowserContext_State", "[Generation] > 0 AND [Revision] > 0 AND [ObservedAt] > 0 AND [ExpiresAt] > 0 AND [RetainUntil] >= [ExpiresAt]");
                });

            migrationBuilder.CreateTable(
                name: "BrowserSessionAudit",
                schema: "bff",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CorrelationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Generation = table.Column<long>(type: "bigint", nullable: false),
                    OccurredAt = table.Column<long>(type: "bigint", nullable: false),
                    Operation = table.Column<int>(type: "int", nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true, collation: "Latin1_General_100_BIN2"),
                    SourceSession = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true, collation: "Latin1_General_100_BIN2")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BrowserSessionAudit", x => x.Id);
                    table.CheckConstraint("CK_BrowserSessionAudit_State", "[Generation] > 0 AND [OccurredAt] > 0 AND [Operation] IN (1, 2, 3, 4, 5, 6, 7) AND (([Subject] IS NULL AND [SourceSession] IS NULL) OR ([Subject] IS NOT NULL AND [SourceSession] IS NOT NULL))");
                });

            migrationBuilder.CreateTable(
                name: "BrowserSessionBudget",
                schema: "bff",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BrowserSessionBudget", x => x.Id);
                    table.CheckConstraint("CK_BrowserSessionBudget_State", "[Id] = 1 AND [Revision] >= 0");
                });

            migrationBuilder.CreateTable(
                name: "BrowserLoginIntent",
                schema: "bff",
                columns: table => new
                {
                    Digest = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false, collation: "Latin1_General_100_BIN2"),
                    CorrelationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BrowserDigest = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false, collation: "Latin1_General_100_BIN2"),
                    Generation = table.Column<long>(type: "bigint", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<long>(type: "bigint", nullable: false),
                    ObservedAt = table.Column<long>(type: "bigint", nullable: false),
                    ExpiresAt = table.Column<long>(type: "bigint", nullable: false),
                    RetainUntil = table.Column<long>(type: "bigint", nullable: false),
                    State = table.Column<int>(type: "int", nullable: false),
                    ClientId = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false, collation: "Latin1_General_100_BIN2"),
                    NativeStateDigest = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true, collation: "Latin1_General_100_BIN2"),
                    TicketDigest = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true, collation: "Latin1_General_100_BIN2")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BrowserLoginIntent", x => x.Digest);
                    table.CheckConstraint("CK_BrowserLoginIntent_Lifetime", "[Generation] > 0 AND [Revision] > 0 AND [CreatedAt] > 0 AND [ObservedAt] >= [CreatedAt] AND [ExpiresAt] > [CreatedAt] AND [ExpiresAt] - [CreatedAt] <= 3000000000 AND [RetainUntil] >= [ExpiresAt]");
                    table.CheckConstraint("CK_BrowserLoginIntent_State", "[State] IN (1, 2, 3, 4, 5)");
                    table.ForeignKey(
                        name: "FK_BrowserLoginIntent_BrowserContext_BrowserDigest",
                        column: x => x.BrowserDigest,
                        principalSchema: "bff",
                        principalTable: "BrowserContext",
                        principalColumn: "Digest",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BrowserLogoutContinuation",
                schema: "bff",
                columns: table => new
                {
                    Digest = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false, collation: "Latin1_General_100_BIN2"),
                    CorrelationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BrowserDigest = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false, collation: "Latin1_General_100_BIN2"),
                    Generation = table.Column<long>(type: "bigint", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<long>(type: "bigint", nullable: false),
                    ObservedAt = table.Column<long>(type: "bigint", nullable: false),
                    ExpiresAt = table.Column<long>(type: "bigint", nullable: false),
                    RetainUntil = table.Column<long>(type: "bigint", nullable: false),
                    State = table.Column<int>(type: "int", nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false, collation: "Latin1_General_100_BIN2"),
                    SourceSession = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false, collation: "Latin1_General_100_BIN2")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BrowserLogoutContinuation", x => x.Digest);
                    table.CheckConstraint("CK_BrowserLogoutContinuation_Lifetime", "[Generation] > 0 AND [Revision] > 0 AND [CreatedAt] > 0 AND [ObservedAt] >= [CreatedAt] AND [ExpiresAt] > [CreatedAt] AND [ExpiresAt] - [CreatedAt] <= 3000000000 AND [RetainUntil] >= [ExpiresAt]");
                    table.CheckConstraint("CK_BrowserLogoutContinuation_State", "[State] IN (1, 2, 3, 4)");
                    table.ForeignKey(
                        name: "FK_BrowserLogoutContinuation_BrowserContext_BrowserDigest",
                        column: x => x.BrowserDigest,
                        principalSchema: "bff",
                        principalTable: "BrowserContext",
                        principalColumn: "Digest",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                schema: "bff",
                table: "BrowserSessionBudget",
                columns: new[] { "Id", "Revision" },
                values: new object[] { 1, 0L });

            migrationBuilder.CreateIndex(
                name: "IX_BrowserContext_RetainUntil",
                schema: "bff",
                table: "BrowserContext",
                column: "RetainUntil");

            migrationBuilder.CreateIndex(
                name: "IX_BrowserLoginIntent_BrowserDigest_Generation",
                schema: "bff",
                table: "BrowserLoginIntent",
                columns: new[] { "BrowserDigest", "Generation" });

            migrationBuilder.CreateIndex(
                name: "IX_BrowserLoginIntent_ExpiresAt_State",
                schema: "bff",
                table: "BrowserLoginIntent",
                columns: new[] { "ExpiresAt", "State" });

            migrationBuilder.CreateIndex(
                name: "IX_BrowserLoginIntent_NativeStateDigest",
                schema: "bff",
                table: "BrowserLoginIntent",
                column: "NativeStateDigest",
                unique: true,
                filter: "[NativeStateDigest] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BrowserLoginIntent_RetainUntil",
                schema: "bff",
                table: "BrowserLoginIntent",
                column: "RetainUntil");

            migrationBuilder.CreateIndex(
                name: "IX_BrowserLogoutContinuation_BrowserDigest_Generation",
                schema: "bff",
                table: "BrowserLogoutContinuation",
                columns: new[] { "BrowserDigest", "Generation" });

            migrationBuilder.CreateIndex(
                name: "IX_BrowserLogoutContinuation_ExpiresAt_State",
                schema: "bff",
                table: "BrowserLogoutContinuation",
                columns: new[] { "ExpiresAt", "State" });

            migrationBuilder.CreateIndex(
                name: "IX_BrowserLogoutContinuation_RetainUntil",
                schema: "bff",
                table: "BrowserLogoutContinuation",
                column: "RetainUntil");

            migrationBuilder.CreateIndex(
                name: "IX_BrowserSessionAudit_CorrelationId_Generation",
                schema: "bff",
                table: "BrowserSessionAudit",
                columns: new[] { "CorrelationId", "Generation" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BrowserLoginIntent",
                schema: "bff");

            migrationBuilder.DropTable(
                name: "BrowserLogoutContinuation",
                schema: "bff");

            migrationBuilder.DropTable(
                name: "BrowserSessionAudit",
                schema: "bff");

            migrationBuilder.DropTable(
                name: "BrowserSessionBudget",
                schema: "bff");

            migrationBuilder.DropTable(
                name: "BrowserContext",
                schema: "bff");
        }
    }
}
