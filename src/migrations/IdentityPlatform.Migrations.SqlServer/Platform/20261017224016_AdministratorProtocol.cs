using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IdentityPlatform.Migrations.SqlServer.Platform
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
                    Digest = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false, collation: "Latin1_General_100_BIN2"),
                    CreatedAt = table.Column<long>(type: "bigint", nullable: false),
                    ExpiresAt = table.Column<long>(type: "bigint", nullable: false),
                    RetainUntil = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdministratorClientAssertionUse", x => x.Digest);
                    table.CheckConstraint("CK_AdministratorClientAssertionUse_Lifetime", "[CreatedAt] >= 0 AND [ExpiresAt] > [CreatedAt] AND [RetainUntil] = [ExpiresAt] + 30");
                });

            migrationBuilder.CreateTable(
                name: "AdministratorProtocolProfile",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    ApplicationId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false, collation: "Latin1_General_100_BIN2"),
                    ScopeId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false, collation: "Latin1_General_100_BIN2"),
                    RedirectUri = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false, collation: "Latin1_General_100_BIN2"),
                    PublicKeyFingerprint = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false, collation: "Latin1_General_100_BIN2"),
                    Enabled = table.Column<bool>(type: "bit", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    LockRevision = table.Column<long>(type: "bigint", nullable: false),
                    ObservedAt = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdministratorProtocolProfile", x => x.Id);
                    table.CheckConstraint("CK_AdministratorProtocolProfile_State", "[Id] = 1 AND [Revision] > 0 AND [LockRevision] >= 0 AND [ObservedAt] >= 0");
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
