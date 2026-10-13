using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IdentityPlatform.Migrations.PostgreSql.Platform
{
    /// <inheritdoc />
    public partial class AdministratorAuthority : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "AuthorityRevision",
                schema: "platform",
                table: "AdministratorGovernance",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.UpdateData(
                schema: "platform",
                table: "AdministratorGovernance",
                keyColumn: "Id",
                keyValue: 1,
                column: "AuthorityRevision",
                value: 0L);

            migrationBuilder.AddCheckConstraint(
                name: "CK_AdministratorGovernance_AuthorityRevision",
                schema: "platform",
                table: "AdministratorGovernance",
                sql: "\"AuthorityRevision\" >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_AdministratorGovernance_AuthorityRevision",
                schema: "platform",
                table: "AdministratorGovernance");

            migrationBuilder.DropColumn(
                name: "AuthorityRevision",
                schema: "platform",
                table: "AdministratorGovernance");
        }
    }
}
