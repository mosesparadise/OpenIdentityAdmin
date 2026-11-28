using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IdentityPlatform.Migrations.PostgreSql.Platform
{
    /// <inheritdoc />
    public partial class ManagedBrowserOrigins : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApplicationBrowserOrigin",
                schema: "platform",
                columns: table => new
                {
                    ApplicationId = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    Slot = table.Column<int>(type: "integer", nullable: false),
                    Origin = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationBrowserOrigin", x => new { x.ApplicationId, x.Slot });
                    table.CheckConstraint("CK_ApplicationBrowserOrigin_Slot", "\"Slot\" >= 0 AND \"Slot\" < 10");
                    table.ForeignKey(
                        name: "FK_ApplicationBrowserOrigin_ApplicationPolicy_ApplicationId",
                        column: x => x.ApplicationId,
                        principalSchema: "platform",
                        principalTable: "ApplicationPolicy",
                        principalColumn: "ApplicationId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationBrowserOrigin_Origin_ApplicationId",
                schema: "platform",
                table: "ApplicationBrowserOrigin",
                columns: new[] { "Origin", "ApplicationId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""DO $$ BEGIN IF EXISTS (SELECT 1 FROM platform."ApplicationBrowserOrigin") THEN RAISE EXCEPTION 'Registered browser origins prevent downgrade'; END IF; END $$""");
            migrationBuilder.DropTable(
                name: "ApplicationBrowserOrigin",
                schema: "platform");
        }
    }
}
