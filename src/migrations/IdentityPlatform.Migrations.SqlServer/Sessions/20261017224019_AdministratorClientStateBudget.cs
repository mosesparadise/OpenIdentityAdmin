using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IdentityPlatform.Migrations.SqlServer.Sessions
{
    /// <inheritdoc />
    public partial class AdministratorClientStateBudget : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AdministratorClientStateBudget",
                schema: "bff",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdministratorClientStateBudget", x => x.Id);
                    table.CheckConstraint("CK_AdministratorClientStateBudget_State", "[Id] = 1 AND [Revision] >= 0");
                });

            migrationBuilder.InsertData(
                schema: "bff",
                table: "AdministratorClientStateBudget",
                columns: new[] { "Id", "Revision" },
                values: new object[] { 1, 0L });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdministratorClientStateBudget",
                schema: "bff");
        }
    }
}
