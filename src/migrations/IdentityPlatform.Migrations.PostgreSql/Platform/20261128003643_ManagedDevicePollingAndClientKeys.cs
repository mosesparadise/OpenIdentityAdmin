using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IdentityPlatform.Migrations.PostgreSql.Platform
{
    /// <inheritdoc />
    public partial class ManagedDevicePollingAndClientKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApplicationAssertionUse",
                schema: "platform",
                columns: table => new
                {
                    ApplicationId = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    Digest = table.Column<string>(type: "character varying(64)", unicode: false, maxLength: 64, nullable: false),
                    CredentialRevision = table.Column<long>(type: "bigint", nullable: false),
                    RetainUntil = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationAssertionUse", x => new { x.ApplicationId, x.Digest });
                    table.CheckConstraint("CK_ApplicationAssertionUse_Time", "\"CredentialRevision\" > 0 AND \"RetainUntil\" > 0");
                    table.ForeignKey(
                        name: "FK_ApplicationAssertionUse_ApplicationPolicy_ApplicationId",
                        column: x => x.ApplicationId,
                        principalSchema: "platform",
                        principalTable: "ApplicationPolicy",
                        principalColumn: "ApplicationId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ApplicationKeyApproval",
                schema: "platform",
                columns: table => new
                {
                    ApplicationId = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    CredentialRevision = table.Column<long>(type: "bigint", nullable: false),
                    Fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Thumbprint = table.Column<string>(type: "character varying(64)", unicode: false, maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationKeyApproval", x => x.ApplicationId);
                    table.CheckConstraint("CK_ApplicationKeyApproval_Revision", "\"CredentialRevision\" > 0");
                    table.ForeignKey(
                        name: "FK_ApplicationKeyApproval_ApplicationPolicy_ApplicationId",
                        column: x => x.ApplicationId,
                        principalSchema: "platform",
                        principalTable: "ApplicationPolicy",
                        principalColumn: "ApplicationId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DevicePollingWindow",
                schema: "platform",
                columns: table => new
                {
                    Digest = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ApplicationId = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    ApplicationRevision = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<long>(type: "bigint", nullable: false),
                    ObservedAt = table.Column<long>(type: "bigint", nullable: false),
                    NextPollAt = table.Column<long>(type: "bigint", nullable: false),
                    ExpiresAt = table.Column<long>(type: "bigint", nullable: false),
                    IntervalSeconds = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DevicePollingWindow", x => x.Digest);
                    table.CheckConstraint("CK_DevicePollingWindow_Cadence", "\"ApplicationRevision\" > 0 AND \"IntervalSeconds\" BETWEEN 5 AND 600");
                    table.CheckConstraint("CK_DevicePollingWindow_Time", "\"CreatedAt\" >= 0 AND \"CreatedAt\" <= \"ObservedAt\" AND \"ObservedAt\" <= \"NextPollAt\" AND \"NextPollAt\" <= \"ExpiresAt\" AND \"ExpiresAt\" - \"CreatedAt\" = 600");
                    table.ForeignKey(
                        name: "FK_DevicePollingWindow_ApplicationPolicy_ApplicationId",
                        column: x => x.ApplicationId,
                        principalSchema: "platform",
                        principalTable: "ApplicationPolicy",
                        principalColumn: "ApplicationId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationAssertionUse_RetainUntil",
                schema: "platform",
                table: "ApplicationAssertionUse",
                column: "RetainUntil");

            migrationBuilder.CreateIndex(
                name: "IX_DevicePollingWindow_ApplicationId",
                schema: "platform",
                table: "DevicePollingWindow",
                column: "ApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_DevicePollingWindow_ExpiresAt",
                schema: "platform",
                table: "DevicePollingWindow",
                column: "ExpiresAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("LOCK TABLE \"platform\".\"ApplicationAssertionUse\", \"platform\".\"ApplicationKeyApproval\", \"platform\".\"DevicePollingWindow\" IN ACCESS EXCLUSIVE MODE; DO $$ BEGIN IF EXISTS (SELECT 1 FROM \"platform\".\"ApplicationAssertionUse\") OR EXISTS (SELECT 1 FROM \"platform\".\"ApplicationKeyApproval\") OR EXISTS (SELECT 1 FROM \"platform\".\"DevicePollingWindow\") THEN RAISE EXCEPTION 'Device or client key records must be retained'; END IF; END $$;");

            migrationBuilder.DropTable(
                name: "ApplicationAssertionUse",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "ApplicationKeyApproval",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "DevicePollingWindow",
                schema: "platform");
        }
    }
}
