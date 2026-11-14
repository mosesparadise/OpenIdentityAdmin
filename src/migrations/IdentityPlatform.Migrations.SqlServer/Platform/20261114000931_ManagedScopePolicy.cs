using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IdentityPlatform.Migrations.SqlServer.Platform
{
    /// <inheritdoc />
    public partial class ManagedScopePolicy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PolicyDigest",
                schema: "platform",
                table: "ManagedLoginInteraction",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true,
                collation: "Latin1_General_100_BIN2");

            migrationBuilder.AddColumn<string>(
                name: "PolicySnapshot",
                schema: "platform",
                table: "ManagedLoginInteraction",
                type: "nvarchar(max)",
                maxLength: 16384,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ManagedScopeReference",
                schema: "platform",
                columns: table => new
                {
                    SelectorDigest = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false, collation: "Latin1_General_100_BIN2"),
                    ScopeId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    AudienceId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true, collation: "Latin1_General_100_BIN2")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ManagedScopeReference", x => new { x.SelectorDigest, x.ScopeId });
                    table.ForeignKey(
                        name: "FK_ManagedScopeReference_ApiAudience_AudienceId",
                        column: x => x.AudienceId,
                        principalSchema: "platform",
                        principalTable: "ApiAudience",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ManagedScopeReference_ManagedLoginInteraction_SelectorDigest",
                        column: x => x.SelectorDigest,
                        principalSchema: "platform",
                        principalTable: "ManagedLoginInteraction",
                        principalColumn: "SelectorDigest",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ManagedScopeReference_OpenIddictScopes_ScopeId",
                        column: x => x.ScopeId,
                        principalSchema: "platform",
                        principalTable: "OpenIddictScopes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_ManagedLoginInteraction_Policy",
                schema: "platform",
                table: "ManagedLoginInteraction",
                sql: "([PolicySnapshot] IS NULL AND [PolicyDigest] IS NULL) OR ([PolicySnapshot] IS NOT NULL AND [PolicyDigest] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_ManagedScopeReference_AudienceId",
                schema: "platform",
                table: "ManagedScopeReference",
                column: "AudienceId");

            migrationBuilder.CreateIndex(
                name: "IX_ManagedScopeReference_ScopeId",
                schema: "platform",
                table: "ManagedScopeReference",
                column: "ScopeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ManagedScopeReference",
                schema: "platform");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ManagedLoginInteraction_Policy",
                schema: "platform",
                table: "ManagedLoginInteraction");

            migrationBuilder.DropColumn(
                name: "PolicyDigest",
                schema: "platform",
                table: "ManagedLoginInteraction");

            migrationBuilder.DropColumn(
                name: "PolicySnapshot",
                schema: "platform",
                table: "ManagedLoginInteraction");
        }
    }
}
