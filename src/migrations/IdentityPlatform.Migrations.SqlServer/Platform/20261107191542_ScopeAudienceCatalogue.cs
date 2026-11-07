using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IdentityPlatform.Migrations.SqlServer.Platform
{
    /// <inheritdoc />
    public partial class ScopeAudienceCatalogue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApiAudience",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, collation: "Latin1_General_100_BIN2"),
                    Resource = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false, collation: "Latin1_General_100_BIN2"),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Owner = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Enabled = table.Column<bool>(type: "bit", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApiAudience", x => x.Id);
                    table.CheckConstraint("CK_ApiAudience_Revision", "[Revision] > 0");
                });

            migrationBuilder.CreateTable(
                name: "CatalogueDeletion",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false, collation: "Latin1_General_100_BIN2"),
                    TargetId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, collation: "Latin1_General_100_BIN2"),
                    TargetRevision = table.Column<long>(type: "bigint", nullable: false),
                    ActorId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false, collation: "Latin1_General_100_BIN2"),
                    SecurityStamp = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false, collation: "Latin1_General_100_BIN2"),
                    AuthenticationGeneration = table.Column<long>(type: "bigint", nullable: false),
                    FactorGeneration = table.Column<long>(type: "bigint", nullable: false),
                    EnrollmentRevision = table.Column<long>(type: "bigint", nullable: false),
                    FactorVerifiedAt = table.Column<long>(type: "bigint", nullable: false),
                    ApprovalUntil = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<long>(type: "bigint", nullable: false),
                    ObservedAt = table.Column<long>(type: "bigint", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    RequestKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ScopedKeyHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false, collation: "Latin1_General_100_BIN2"),
                    RequestHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false, collation: "Latin1_General_100_BIN2"),
                    AdmissionAuditId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompletionAuditId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    State = table.Column<int>(type: "int", nullable: false),
                    Failure = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatalogueDeletion", x => x.Id);
                    table.CheckConstraint("CK_CatalogueDeletion_Authority", "[AuthenticationGeneration] > 0 AND [FactorGeneration] > 0 AND [EnrollmentRevision] >= 0");
                    table.CheckConstraint("CK_CatalogueDeletion_State", "[State] IN (1, 2, 3) AND [Failure] IN (0, 1, 2, 3, 4, 5) AND (([State] = 3 AND [Failure] <> 0) OR ([State] <> 3 AND [Failure] = 0)) AND (([State] = 1 AND [CompletionAuditId] IS NULL) OR ([State] <> 1 AND [CompletionAuditId] IS NOT NULL)) AND ([CompletionAuditId] IS NULL OR [CompletionAuditId] <> [AdmissionAuditId])");
                    table.CheckConstraint("CK_CatalogueDeletion_Target", "[Kind] IN ('scope', 'audience') AND [Revision] > 0 AND [TargetRevision] > 0");
                    table.CheckConstraint("CK_CatalogueDeletion_Time", "[FactorVerifiedAt] >= 0 AND [CreatedAt] >= [FactorVerifiedAt] AND [ObservedAt] >= [CreatedAt] AND [ApprovalUntil] > [CreatedAt] AND [ApprovalUntil] - [FactorVerifiedAt] <= 300");
                    table.ForeignKey(
                        name: "FK_CatalogueDeletion_SecurityAuditIntent_AdmissionAuditId",
                        column: x => x.AdmissionAuditId,
                        principalSchema: "platform",
                        principalTable: "SecurityAuditIntent",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CatalogueDeletion_SecurityAuditIntent_CompletionAuditId",
                        column: x => x.CompletionAuditId,
                        principalSchema: "platform",
                        principalTable: "SecurityAuditIntent",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ScopePolicy",
                schema: "platform",
                columns: table => new
                {
                    ScopeId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false, collation: "Latin1_General_100_BIN2"),
                    Enabled = table.Column<bool>(type: "bit", nullable: false),
                    ConsentText = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Disclosure = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false, collation: "Latin1_General_100_BIN2"),
                    Revision = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScopePolicy", x => x.ScopeId);
                    table.CheckConstraint("CK_ScopePolicy_Disclosure", "[Kind] IN ('identity', 'api') AND [Disclosure] IN ('none', 'email') AND ([Kind] = 'identity' OR [Disclosure] = 'none')");
                    table.CheckConstraint("CK_ScopePolicy_Revision", "[Revision] > 0");
                    table.ForeignKey(
                        name: "FK_ScopePolicy_OpenIddictScopes_ScopeId",
                        column: x => x.ScopeId,
                        principalSchema: "platform",
                        principalTable: "OpenIddictScopes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApiAudience_Resource",
                schema: "platform",
                table: "ApiAudience",
                column: "Resource",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CatalogueDeletion_AdmissionAuditId",
                schema: "platform",
                table: "CatalogueDeletion",
                column: "AdmissionAuditId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CatalogueDeletion_CompletionAuditId",
                schema: "platform",
                table: "CatalogueDeletion",
                column: "CompletionAuditId",
                unique: true,
                filter: "[CompletionAuditId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CatalogueDeletion_Kind_TargetId_State",
                schema: "platform",
                table: "CatalogueDeletion",
                columns: new[] { "Kind", "TargetId", "State" });

            migrationBuilder.CreateIndex(
                name: "IX_CatalogueDeletion_ScopedKeyHash",
                schema: "platform",
                table: "CatalogueDeletion",
                column: "ScopedKeyHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CatalogueDeletion_State_ObservedAt_Id",
                schema: "platform",
                table: "CatalogueDeletion",
                columns: new[] { "State", "ObservedAt", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApiAudience",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "CatalogueDeletion",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "ScopePolicy",
                schema: "platform");
        }
    }
}
