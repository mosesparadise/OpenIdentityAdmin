using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IdentityPlatform.Migrations.PostgreSql.Platform
{
    /// <inheritdoc />
    public partial class AdministratorFactors : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "FactorGeneration",
                schema: "platform",
                table: "AdministratorEnrollment",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "RecoveryGeneration",
                schema: "platform",
                table: "AdministratorEnrollment",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<bool>(
                name: "Suspended",
                schema: "platform",
                table: "AdministratorEnrollment",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "AdministratorAdmission",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdministratorAdmission", x => x.Id);
                    table.CheckConstraint("CK_AdministratorAdmission_Singleton", "\"Id\" = 1 AND \"Revision\" >= 0");
                });

            migrationBuilder.CreateTable(
                name: "AdministratorAuthentication",
                schema: "platform",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    Generation = table.Column<long>(type: "bigint", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    Failures = table.Column<int>(type: "integer", nullable: false),
                    LockoutUntil = table.Column<long>(type: "bigint", nullable: true),
                    ObservedAt = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdministratorAuthentication", x => x.UserId);
                    table.CheckConstraint("CK_AdministratorAuthentication_State", "\"Generation\" > 0 AND \"Revision\" >= 0 AND \"Failures\" >= 0 AND \"Failures\" <= 5");
                    table.ForeignKey(
                        name: "FK_AdministratorAuthentication_AdministratorEnrollment_UserId",
                        column: x => x.UserId,
                        principalSchema: "platform",
                        principalTable: "AdministratorEnrollment",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AdministratorBrowserBudget",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    ExpiresAt = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdministratorBrowserBudget", x => x.Id);
                    table.CheckConstraint("CK_AdministratorBrowserBudget_State", "\"Revision\" >= 0 AND \"ExpiresAt\" > 0");
                });

            migrationBuilder.CreateTable(
                name: "AdministratorFactor",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    Generation = table.Column<long>(type: "bigint", nullable: false),
                    ProtectedSeed = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdministratorFactor", x => x.Id);
                    table.CheckConstraint("CK_AdministratorFactor_Generation", "\"Generation\" > 0");
                    table.ForeignKey(
                        name: "FK_AdministratorFactor_AdministratorAuthentication_UserId",
                        column: x => x.UserId,
                        principalSchema: "platform",
                        principalTable: "AdministratorAuthentication",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AdministratorRecoveryCode",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    Generation = table.Column<long>(type: "bigint", nullable: false),
                    Verifier = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RedeemedAt = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdministratorRecoveryCode", x => x.Id);
                    table.CheckConstraint("CK_AdministratorRecoveryCode_State", "\"Generation\" > 0 AND (\"RedeemedAt\" IS NULL OR \"RedeemedAt\" >= 0)");
                    table.ForeignKey(
                        name: "FK_AdministratorRecoveryCode_AdministratorAuthentication_UserId",
                        column: x => x.UserId,
                        principalSchema: "platform",
                        principalTable: "AdministratorAuthentication",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AdministratorReplay",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    Generation = table.Column<long>(type: "bigint", nullable: false),
                    ProtectedCode = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    AcceptedAt = table.Column<long>(type: "bigint", nullable: false),
                    ExpiresAt = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdministratorReplay", x => x.Id);
                    table.CheckConstraint("CK_AdministratorReplay_State", "\"Generation\" > 0 AND \"AcceptedAt\" >= 0 AND \"ExpiresAt\" > \"AcceptedAt\"");
                    table.ForeignKey(
                        name: "FK_AdministratorReplay_AdministratorAuthentication_UserId",
                        column: x => x.UserId,
                        principalSchema: "platform",
                        principalTable: "AdministratorAuthentication",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AdministratorSession",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PublicId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    AuthenticationMethods = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SecurityStamp = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Generation = table.Column<long>(type: "bigint", nullable: false),
                    FactorGeneration = table.Column<long>(type: "bigint", nullable: false),
                    PasswordVerifiedAt = table.Column<long>(type: "bigint", nullable: false),
                    FactorVerifiedAt = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<long>(type: "bigint", nullable: false),
                    LastActivityAt = table.Column<long>(type: "bigint", nullable: false),
                    ExpiresAt = table.Column<long>(type: "bigint", nullable: false),
                    Revoked = table.Column<bool>(type: "boolean", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdministratorSession", x => x.Id);
                    table.CheckConstraint("CK_AdministratorSession_State", "\"Generation\" > 0 AND \"FactorGeneration\" > 0 AND \"Revision\" >= 0 AND \"CreatedAt\" >= 0 AND \"LastActivityAt\" >= \"CreatedAt\" AND \"ExpiresAt\" > \"LastActivityAt\" AND \"AuthenticationMethods\" = 'pwd otp' AND \"PasswordVerifiedAt\" >= 0 AND \"FactorVerifiedAt\" >= 0 AND \"PasswordVerifiedAt\" <= \"FactorVerifiedAt\" AND \"FactorVerifiedAt\" <= \"CreatedAt\"");
                    table.CheckConstraint("CK_AdministratorSession_MethodLength", "octet_length(\"AuthenticationMethods\") = 7");
                    table.ForeignKey(
                        name: "FK_AdministratorSession_AdministratorAuthentication_UserId",
                        column: x => x.UserId,
                        principalSchema: "platform",
                        principalTable: "AdministratorAuthentication",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AdministratorInteraction",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PublicId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    BrowserBudgetId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    BrowserDigest = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Purpose = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    State = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SecurityStamp = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Generation = table.Column<long>(type: "bigint", nullable: false),
                    DestinationGeneration = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<long>(type: "bigint", nullable: false),
                    ExpiresAt = table.Column<long>(type: "bigint", nullable: false),
                    PasswordVerifiedAt = table.Column<long>(type: "bigint", nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    StagedSeed = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdministratorInteraction", x => x.Id);
                    table.CheckConstraint("CK_AdministratorInteraction_State", "\"Generation\" > 0 AND \"DestinationGeneration\" > 0 AND \"Revision\" >= 0 AND \"Attempts\" >= 0 AND \"Attempts\" <= 5 AND \"CreatedAt\" >= 0 AND \"ExpiresAt\" > \"CreatedAt\"");
                    table.ForeignKey(
                        name: "FK_AdministratorInteraction_AdministratorAuthentication_UserId",
                        column: x => x.UserId,
                        principalSchema: "platform",
                        principalTable: "AdministratorAuthentication",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AdministratorInteraction_AdministratorBrowserBudget_Browser~",
                        column: x => x.BrowserBudgetId,
                        principalSchema: "platform",
                        principalTable: "AdministratorBrowserBudget",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                schema: "platform",
                table: "AdministratorAdmission",
                columns: new[] { "Id", "Revision" },
                values: new object[] { 1, 0L });

            migrationBuilder.AddCheckConstraint(
                name: "CK_AdministratorEnrollment_Generations",
                schema: "platform",
                table: "AdministratorEnrollment",
                sql: "\"FactorGeneration\" >= 0 AND \"RecoveryGeneration\" >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_AdministratorBrowserBudget_ExpiresAt",
                schema: "platform",
                table: "AdministratorBrowserBudget",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_AdministratorFactor_UserId_Generation",
                schema: "platform",
                table: "AdministratorFactor",
                columns: new[] { "UserId", "Generation" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AdministratorInteraction_BrowserBudgetId",
                schema: "platform",
                table: "AdministratorInteraction",
                column: "BrowserBudgetId");

            migrationBuilder.CreateIndex(
                name: "IX_AdministratorInteraction_ExpiresAt",
                schema: "platform",
                table: "AdministratorInteraction",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_AdministratorInteraction_PublicId",
                schema: "platform",
                table: "AdministratorInteraction",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AdministratorInteraction_UserId_Generation",
                schema: "platform",
                table: "AdministratorInteraction",
                columns: new[] { "UserId", "Generation" });

            migrationBuilder.CreateIndex(
                name: "IX_AdministratorRecoveryCode_UserId_Generation_Verifier",
                schema: "platform",
                table: "AdministratorRecoveryCode",
                columns: new[] { "UserId", "Generation", "Verifier" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AdministratorReplay_ExpiresAt",
                schema: "platform",
                table: "AdministratorReplay",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_AdministratorReplay_UserId_Generation",
                schema: "platform",
                table: "AdministratorReplay",
                columns: new[] { "UserId", "Generation" });

            migrationBuilder.CreateIndex(
                name: "IX_AdministratorSession_ExpiresAt",
                schema: "platform",
                table: "AdministratorSession",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_AdministratorSession_PublicId",
                schema: "platform",
                table: "AdministratorSession",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AdministratorSession_UserId_Generation",
                schema: "platform",
                table: "AdministratorSession",
                columns: new[] { "UserId", "Generation" });

            // Backfill inert Pending state only; no factor, enabled user, session or recovery authority.
            migrationBuilder.Sql("""
                INSERT INTO "platform"."AdministratorAuthentication" ("UserId", "Generation", "Revision", "Failures")
                SELECT e."UserId", 1, 0, 0 FROM "platform"."AdministratorEnrollment" e
                JOIN "platform"."AdministratorBootstrapReceipt" r ON r."UserId" = e."UserId"
                JOIN "platform"."AspNetUsers" u ON u."Id" = e."UserId"
                WHERE e."State" = 'Pending' AND u."IsEnabled" = FALSE AND u."TwoFactorEnabled" = FALSE
                AND EXISTS (SELECT 1 FROM "platform"."AdministratorGovernance" g WHERE g."Id" = 1 AND g."BootstrapConsumed" = TRUE)
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdministratorAdmission",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "AdministratorFactor",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "AdministratorInteraction",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "AdministratorRecoveryCode",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "AdministratorReplay",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "AdministratorSession",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "AdministratorBrowserBudget",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "AdministratorAuthentication",
                schema: "platform");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AdministratorEnrollment_Generations",
                schema: "platform",
                table: "AdministratorEnrollment");

            migrationBuilder.DropColumn(
                name: "FactorGeneration",
                schema: "platform",
                table: "AdministratorEnrollment");

            migrationBuilder.DropColumn(
                name: "RecoveryGeneration",
                schema: "platform",
                table: "AdministratorEnrollment");

            migrationBuilder.DropColumn(
                name: "Suspended",
                schema: "platform",
                table: "AdministratorEnrollment");
        }
    }
}
