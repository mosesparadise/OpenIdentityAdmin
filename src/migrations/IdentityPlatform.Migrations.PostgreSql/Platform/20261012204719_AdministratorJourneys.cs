using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IdentityPlatform.Migrations.PostgreSql.Platform
{
    /// <inheritdoc />
    public partial class AdministratorJourneys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "ExpectedEnrollmentRevision",
                schema: "platform",
                table: "AdministratorInteraction",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "LastAllocatedFactorGeneration",
                schema: "platform",
                table: "AdministratorAuthentication",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.Sql("""
                UPDATE platform."AdministratorAuthentication" AS auth
                SET "LastAllocatedFactorGeneration" = GREATEST(
                    COALESCE((SELECT MAX("FactorGeneration") FROM platform."AdministratorEnrollment" WHERE "UserId" = auth."UserId"), 0),
                    COALESCE((SELECT MAX("Generation") FROM platform."AdministratorFactor" WHERE "UserId" = auth."UserId"), 0),
                    COALESCE((SELECT MAX("DestinationGeneration") FROM platform."AdministratorInteraction" WHERE "UserId" = auth."UserId"), 0),
                    COALESCE((SELECT MAX("Generation") FROM platform."AdministratorReplay" WHERE "UserId" = auth."UserId"), 0));
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_AdministratorInteraction_EnrollmentRevision",
                schema: "platform",
                table: "AdministratorInteraction",
                sql: "\"ExpectedEnrollmentRevision\" IS NULL OR \"ExpectedEnrollmentRevision\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AdministratorAuthentication_FactorAllocator",
                schema: "platform",
                table: "AdministratorAuthentication",
                sql: "\"LastAllocatedFactorGeneration\" >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_AdministratorInteraction_EnrollmentRevision",
                schema: "platform",
                table: "AdministratorInteraction");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AdministratorAuthentication_FactorAllocator",
                schema: "platform",
                table: "AdministratorAuthentication");

            migrationBuilder.DropColumn(
                name: "ExpectedEnrollmentRevision",
                schema: "platform",
                table: "AdministratorInteraction");

            migrationBuilder.DropColumn(
                name: "LastAllocatedFactorGeneration",
                schema: "platform",
                table: "AdministratorAuthentication");
        }
    }
}
