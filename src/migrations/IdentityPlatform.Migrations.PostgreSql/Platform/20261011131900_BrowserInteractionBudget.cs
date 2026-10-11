using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IdentityPlatform.Migrations.PostgreSql.Platform
{
    /// <inheritdoc />
    public partial class BrowserInteractionBudget : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BudgetId",
                schema: "platform",
                table: "LoginInteraction",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "LoginBrowserBudget",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    LastAdmissionAt = table.Column<long>(type: "bigint", nullable: false),
                    ExpiresAt = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoginBrowserBudget", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LoginInteraction_BudgetId_Consumed_ExpiresAt",
                schema: "platform",
                table: "LoginInteraction",
                columns: new[] { "BudgetId", "Consumed", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_LoginBrowserBudget_ExpiresAt",
                schema: "platform",
                table: "LoginBrowserBudget",
                column: "ExpiresAt");

            migrationBuilder.AddForeignKey(
                name: "FK_LoginInteraction_LoginBrowserBudget_BudgetId",
                schema: "platform",
                table: "LoginInteraction",
                column: "BudgetId",
                principalSchema: "platform",
                principalTable: "LoginBrowserBudget",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LoginInteraction_LoginBrowserBudget_BudgetId",
                schema: "platform",
                table: "LoginInteraction");

            migrationBuilder.DropTable(
                name: "LoginBrowserBudget",
                schema: "platform");

            migrationBuilder.DropIndex(
                name: "IX_LoginInteraction_BudgetId_Consumed_ExpiresAt",
                schema: "platform",
                table: "LoginInteraction");

            migrationBuilder.DropColumn(
                name: "BudgetId",
                schema: "platform",
                table: "LoginInteraction");
        }
    }
}
