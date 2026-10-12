using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IdentityPlatform.Migrations.PostgreSql.Platform
{
    /// <inheritdoc />
    public partial class AdministratorBootstrap : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AdministratorBootstrapReceipt",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    OperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    OriginalNormalizedUserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    BundleVersion = table.Column<int>(type: "integer", nullable: false),
                    CompletedAt = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdministratorBootstrapReceipt", x => x.Id);
                    table.CheckConstraint("CK_AdministratorBootstrapReceipt_Singleton", "\"Id\" = 1");
                });

            migrationBuilder.CreateTable(
                name: "AdministratorEnrollment",
                schema: "platform",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    State = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdministratorEnrollment", x => x.UserId);
                    table.CheckConstraint("CK_AdministratorEnrollment_Revision", "\"Revision\" >= 0");
                });

            migrationBuilder.CreateTable(
                name: "AdministratorGovernance",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    BootstrapConsumed = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdministratorGovernance", x => x.Id);
                    table.CheckConstraint("CK_AdministratorGovernance_Revision", "\"Revision\" >= 0");
                    table.CheckConstraint("CK_AdministratorGovernance_Singleton", "\"Id\" = 1");
                });

            migrationBuilder.InsertData(
                schema: "platform",
                table: "AdministratorGovernance",
                columns: new[] { "Id", "BootstrapConsumed", "Revision" },
                values: new object[] { 1, false, 0L });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdministratorBootstrapReceipt",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "AdministratorEnrollment",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "AdministratorGovernance",
                schema: "platform");
        }
    }
}
