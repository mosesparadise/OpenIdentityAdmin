using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IdentityPlatform.Migrations.PostgreSql.Platform
{
    /// <inheritdoc />
    public partial class PilotProtocolGate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PilotProtocolGate",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PilotProtocolGate", x => x.Id);
                });

            migrationBuilder.InsertData(
                schema: "platform",
                table: "PilotProtocolGate",
                column: "Id",
                value: "first-party-v1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PilotProtocolGate",
                schema: "platform");
        }
    }
}
