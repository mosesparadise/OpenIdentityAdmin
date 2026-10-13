using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IdentityPlatform.Migrations.PostgreSql.Platform
{
    /// <inheritdoc />
    public partial class AdministratorRecovery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AuthorizationKind",
                schema: "platform",
                table: "AdministratorInteraction",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "AuthorizationVerifiedAt",
                schema: "platform",
                table: "AdministratorInteraction",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SessionPublicId",
                schema: "platform",
                table: "AdministratorInteraction",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "SourceFactorGeneration",
                schema: "platform",
                table: "AdministratorInteraction",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_AdministratorInteraction_Authorization",
                schema: "platform",
                table: "AdministratorInteraction",
                sql: "(\"AuthorizationKind\" IS NULL AND \"AuthorizationVerifiedAt\" IS NULL AND \"SourceFactorGeneration\" IS NULL) OR (\"AuthorizationKind\" IS NOT NULL AND \"AuthorizationVerifiedAt\" IS NOT NULL AND \"SourceFactorGeneration\" IS NOT NULL AND \"AuthorizationVerifiedAt\" >= \"PasswordVerifiedAt\" AND \"SourceFactorGeneration\" > 0)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_AdministratorInteraction_Authorization",
                schema: "platform",
                table: "AdministratorInteraction");

            migrationBuilder.DropColumn(
                name: "AuthorizationKind",
                schema: "platform",
                table: "AdministratorInteraction");

            migrationBuilder.DropColumn(
                name: "AuthorizationVerifiedAt",
                schema: "platform",
                table: "AdministratorInteraction");

            migrationBuilder.DropColumn(
                name: "SessionPublicId",
                schema: "platform",
                table: "AdministratorInteraction");

            migrationBuilder.DropColumn(
                name: "SourceFactorGeneration",
                schema: "platform",
                table: "AdministratorInteraction");
        }
    }
}
