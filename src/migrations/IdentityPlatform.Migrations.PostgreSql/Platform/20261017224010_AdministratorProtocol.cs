using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IdentityPlatform.Migrations.PostgreSql.Platform
{
    /// <inheritdoc />
    public partial class AdministratorProtocol : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AdministratorClientAssertionUse",
                schema: "platform",
                columns: table => new
                {
                    Digest = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false, collation: "C"),
                    CreatedAt = table.Column<long>(type: "bigint", nullable: false),
                    ExpiresAt = table.Column<long>(type: "bigint", nullable: false),
                    RetainUntil = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdministratorClientAssertionUse", x => x.Digest);
                    table.CheckConstraint("CK_AdministratorClientAssertionUse_Lifetime", "\"CreatedAt\" >= 0 AND \"ExpiresAt\" > \"CreatedAt\" AND \"RetainUntil\" = \"ExpiresAt\" + 30");
                });

            migrationBuilder.CreateTable(
                name: "AdministratorProtocolProfile",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    ApplicationId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false, collation: "C"),
                    ScopeId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false, collation: "C"),
                    RedirectUri = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false, collation: "C"),
                    PublicKeyFingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false, collation: "C"),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    LockRevision = table.Column<long>(type: "bigint", nullable: false),
                    ObservedAt = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdministratorProtocolProfile", x => x.Id);
                    table.CheckConstraint("CK_AdministratorProtocolProfile_State", "\"Id\" = 1 AND \"Revision\" > 0 AND \"LockRevision\" >= 0 AND \"ObservedAt\" >= 0");
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdministratorClientAssertionUse_RetainUntil",
                schema: "platform",
                table: "AdministratorClientAssertionUse",
                column: "RetainUntil");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdministratorClientAssertionUse",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "AdministratorProtocolProfile",
                schema: "platform");
        }
    }
}
