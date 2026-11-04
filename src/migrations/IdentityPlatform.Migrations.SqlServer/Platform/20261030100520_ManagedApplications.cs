using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IdentityPlatform.Migrations.SqlServer.Platform
{
    /// <inheritdoc />
    public partial class ManagedApplications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApplicationPolicy",
                schema: "platform",
                columns: table => new
                {
                    ApplicationId = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false, collation: "Latin1_General_100_BIN2"),
                    Profile = table.Column<string>(type: "nvarchar(14)", maxLength: 14, nullable: false, collation: "Latin1_General_100_BIN2"),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    CredentialRevision = table.Column<long>(type: "bigint", nullable: false),
                    CredentialIssuedAt = table.Column<long>(type: "bigint", nullable: false),
                    CredentialExpiresAt = table.Column<long>(type: "bigint", nullable: false),
                    ObservedAt = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationPolicy", x => x.ApplicationId);
                    table.CheckConstraint("CK_ApplicationPolicy_Profile", "[Profile] = 'managed-web-v1'");
                    table.CheckConstraint("CK_ApplicationPolicy_Revision", "[Revision] >= [CredentialRevision] AND [CredentialRevision] > 0");
                    table.CheckConstraint("CK_ApplicationPolicy_Time", "[CredentialIssuedAt] >= 0 AND [CredentialExpiresAt] > [CredentialIssuedAt] AND [ObservedAt] >= [CredentialIssuedAt]");
                });

            migrationBuilder.CreateTable(
                name: "ManagedLoginInteraction",
                schema: "platform",
                columns: table => new
                {
                    SelectorDigest = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false, collation: "Latin1_General_100_BIN2"),
                    BrowserDigest = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false, collation: "Latin1_General_100_BIN2"),
                    ApplicationId = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false, collation: "Latin1_General_100_BIN2"),
                    DemandDigest = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false, collation: "Latin1_General_100_BIN2"),
                    RequestId = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true, collation: "Latin1_General_100_BIN2"),
                    ApplicationRevision = table.Column<long>(type: "bigint", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    State = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<long>(type: "bigint", nullable: false),
                    ExpiresAt = table.Column<long>(type: "bigint", nullable: false),
                    ObservedAt = table.Column<long>(type: "bigint", nullable: false),
                    RetainUntil = table.Column<long>(type: "bigint", nullable: false),
                    RequestExpiresAt = table.Column<long>(type: "bigint", nullable: true),
                    NativeDependencyExpiresAt = table.Column<long>(type: "bigint", nullable: true),
                    NativeDependencyCreatedAt = table.Column<long>(type: "bigint", nullable: true),
                    CorrelationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CeremonyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CeremonyBrowserDigest = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true, collation: "Latin1_General_100_BIN2"),
                    BrowserBudgetId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true, collation: "Latin1_General_100_BIN2"),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true, collation: "Latin1_General_100_BIN2"),
                    SecurityStamp = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true, collation: "Latin1_General_100_BIN2"),
                    AuthenticationGeneration = table.Column<long>(type: "bigint", nullable: true),
                    FactorGeneration = table.Column<long>(type: "bigint", nullable: true),
                    EnrollmentRevision = table.Column<long>(type: "bigint", nullable: true),
                    PasswordVerifiedAt = table.Column<long>(type: "bigint", nullable: true),
                    MfaVerifiedAt = table.Column<long>(type: "bigint", nullable: true),
                    ConsentVerifiedAt = table.Column<long>(type: "bigint", nullable: true),
                    AuthorizationId = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true, collation: "Latin1_General_100_BIN2"),
                    CodeId = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true, collation: "Latin1_General_100_BIN2")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ManagedLoginInteraction", x => x.SelectorDigest);
                    table.CheckConstraint("CK_ManagedLoginInteraction_Code", "([State] = 5 AND [AuthorizationId] IS NOT NULL AND [CodeId] IS NOT NULL) OR ([State] <> 5 AND [AuthorizationId] IS NULL AND [CodeId] IS NULL)");
                    table.CheckConstraint("CK_ManagedLoginInteraction_Native", "([RequestId] IS NULL AND [RequestExpiresAt] IS NULL AND [NativeDependencyCreatedAt] IS NULL AND [NativeDependencyExpiresAt] IS NULL) OR ([RequestId] IS NOT NULL AND [RequestExpiresAt] IS NOT NULL AND [NativeDependencyCreatedAt] IS NOT NULL AND [NativeDependencyExpiresAt] IS NOT NULL AND [RequestExpiresAt] >= [ExpiresAt] AND [NativeDependencyCreatedAt] >= [CreatedAt] AND [NativeDependencyExpiresAt] >= [RequestExpiresAt] AND [NativeDependencyExpiresAt] > [NativeDependencyCreatedAt] AND [RetainUntil] >= [NativeDependencyExpiresAt])");
                    table.CheckConstraint("CK_ManagedLoginInteraction_Proof", "[State] IN (6, 7) OR ([State] = 1 AND [CeremonyId] IS NULL AND [CeremonyBrowserDigest] IS NULL AND [BrowserBudgetId] IS NULL AND [UserId] IS NULL AND [SecurityStamp] IS NULL AND [AuthenticationGeneration] IS NULL AND [FactorGeneration] IS NULL AND [EnrollmentRevision] IS NULL AND [PasswordVerifiedAt] IS NULL AND [MfaVerifiedAt] IS NULL AND [ConsentVerifiedAt] IS NULL) OR ([RequestId] IS NOT NULL AND [CeremonyId] IS NOT NULL AND [CeremonyBrowserDigest] IS NOT NULL AND [BrowserBudgetId] IS NOT NULL AND [UserId] IS NOT NULL AND [SecurityStamp] IS NOT NULL AND [AuthenticationGeneration] IS NOT NULL AND [AuthenticationGeneration] > 0 AND [FactorGeneration] IS NOT NULL AND [FactorGeneration] > 0 AND [EnrollmentRevision] IS NOT NULL AND [EnrollmentRevision] >= 0 AND [PasswordVerifiedAt] IS NOT NULL AND [PasswordVerifiedAt] >= [CreatedAt] AND [PasswordVerifiedAt] < [ExpiresAt] AND (([State] = 2 AND [MfaVerifiedAt] IS NULL AND [ConsentVerifiedAt] IS NULL) OR ([State] = 3 AND [MfaVerifiedAt] IS NOT NULL AND [MfaVerifiedAt] >= [PasswordVerifiedAt] AND [MfaVerifiedAt] < [ExpiresAt] AND [ConsentVerifiedAt] IS NULL) OR ([State] IN (4, 5) AND [MfaVerifiedAt] IS NOT NULL AND [MfaVerifiedAt] >= [PasswordVerifiedAt] AND [MfaVerifiedAt] < [ExpiresAt] AND [ConsentVerifiedAt] IS NOT NULL AND [ConsentVerifiedAt] >= [MfaVerifiedAt] AND [ConsentVerifiedAt] < [ExpiresAt])))");
                    table.CheckConstraint("CK_ManagedLoginInteraction_Revision", "[Revision] > 0 AND [ApplicationRevision] > 0");
                    table.CheckConstraint("CK_ManagedLoginInteraction_State", "[State] IN (1, 2, 3, 4, 5, 6, 7)");
                    table.CheckConstraint("CK_ManagedLoginInteraction_Time", "[CreatedAt] >= 0 AND [ExpiresAt] > [CreatedAt] AND [ExpiresAt] - [CreatedAt] <= 300 AND [ObservedAt] >= [CreatedAt] AND [RetainUntil] >= [ExpiresAt]");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationPolicy_CredentialExpiresAt",
                schema: "platform",
                table: "ApplicationPolicy",
                column: "CredentialExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_ManagedLoginInteraction_ApplicationId_ApplicationRevision",
                schema: "platform",
                table: "ManagedLoginInteraction",
                columns: new[] { "ApplicationId", "ApplicationRevision" });

            migrationBuilder.CreateIndex(
                name: "IX_ManagedLoginInteraction_AuthorizationId",
                schema: "platform",
                table: "ManagedLoginInteraction",
                column: "AuthorizationId",
                unique: true,
                filter: "[AuthorizationId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ManagedLoginInteraction_BrowserDigest_State_ExpiresAt",
                schema: "platform",
                table: "ManagedLoginInteraction",
                columns: new[] { "BrowserDigest", "State", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ManagedLoginInteraction_CeremonyId",
                schema: "platform",
                table: "ManagedLoginInteraction",
                column: "CeremonyId",
                unique: true,
                filter: "[CeremonyId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ManagedLoginInteraction_CodeId",
                schema: "platform",
                table: "ManagedLoginInteraction",
                column: "CodeId",
                unique: true,
                filter: "[CodeId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ManagedLoginInteraction_RequestId",
                schema: "platform",
                table: "ManagedLoginInteraction",
                column: "RequestId",
                unique: true,
                filter: "[RequestId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ManagedLoginInteraction_RetainUntil",
                schema: "platform",
                table: "ManagedLoginInteraction",
                column: "RetainUntil");

            migrationBuilder.CreateIndex(
                name: "IX_ManagedLoginInteraction_State_ExpiresAt",
                schema: "platform",
                table: "ManagedLoginInteraction",
                columns: new[] { "State", "ExpiresAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApplicationPolicy",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "ManagedLoginInteraction",
                schema: "platform");
        }
    }
}
