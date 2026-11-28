using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IdentityPlatform.Migrations.SqlServer.Platform
{
    /// <inheritdoc />
    public partial class ManagedDeviceHumanInteraction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ManagedLoginInteraction_Code",
                schema: "platform",
                table: "ManagedLoginInteraction");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ManagedLoginInteraction_Native",
                schema: "platform",
                table: "ManagedLoginInteraction");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ManagedLoginInteraction_Proof",
                schema: "platform",
                table: "ManagedLoginInteraction");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ManagedLoginInteraction_State",
                schema: "platform",
                table: "ManagedLoginInteraction");

            migrationBuilder.AddColumn<string>(
                name: "DeviceEntryId",
                schema: "platform",
                table: "ManagedLoginInteraction",
                type: "nvarchar(400)",
                maxLength: 400,
                nullable: true,
                collation: "Latin1_General_100_BIN2");

            migrationBuilder.AddColumn<long>(
                name: "LastCleanupAttempt",
                schema: "platform",
                table: "DevicePollingWindow",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NativeAuthorizationId",
                schema: "platform",
                table: "DevicePollingWindow",
                type: "nvarchar(400)",
                maxLength: 400,
                nullable: true,
                collation: "Latin1_General_100_BIN2");

            migrationBuilder.CreateIndex(
                name: "IX_ManagedLoginInteraction_DeviceEntryId",
                schema: "platform",
                table: "ManagedLoginInteraction",
                column: "DeviceEntryId",
                unique: true,
                filter: "[DeviceEntryId] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ManagedLoginInteraction_Code",
                schema: "platform",
                table: "ManagedLoginInteraction",
                sql: "([DeviceEntryId] IS NULL AND (([State] = 5 AND [AuthorizationId] IS NOT NULL AND [CodeId] IS NOT NULL) OR ([State] <> 5 AND [AuthorizationId] IS NULL AND [CodeId] IS NULL))) OR ([DeviceEntryId] IS NOT NULL AND [State] <> 5 AND [RequestId] IS NOT NULL AND [AuthorizationId] IS NOT NULL AND [CodeId] IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ManagedLoginInteraction_Native",
                schema: "platform",
                table: "ManagedLoginInteraction",
                sql: "([RequestId] IS NULL AND [RequestExpiresAt] IS NULL AND [NativeDependencyCreatedAt] IS NULL AND [NativeDependencyExpiresAt] IS NULL) OR ([RequestId] IS NOT NULL AND [RequestExpiresAt] IS NOT NULL AND [NativeDependencyCreatedAt] IS NOT NULL AND [NativeDependencyExpiresAt] IS NOT NULL AND [RequestExpiresAt] >= [ExpiresAt] AND (([DeviceEntryId] IS NULL AND [NativeDependencyCreatedAt] >= [CreatedAt]) OR ([DeviceEntryId] IS NOT NULL AND [NativeDependencyCreatedAt] <= [CreatedAt])) AND [NativeDependencyExpiresAt] >= [RequestExpiresAt] AND [NativeDependencyExpiresAt] > [NativeDependencyCreatedAt] AND [RetainUntil] >= [NativeDependencyExpiresAt])");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ManagedLoginInteraction_Proof",
                schema: "platform",
                table: "ManagedLoginInteraction",
                sql: "[State] IN (6, 7) OR ([State] = 1 AND [CeremonyId] IS NULL AND [CeremonyBrowserDigest] IS NULL AND [BrowserBudgetId] IS NULL AND [UserId] IS NULL AND [SecurityStamp] IS NULL AND [AuthenticationGeneration] IS NULL AND [FactorGeneration] IS NULL AND [EnrollmentRevision] IS NULL AND [PasswordVerifiedAt] IS NULL AND [MfaVerifiedAt] IS NULL AND [ConsentVerifiedAt] IS NULL) OR ([RequestId] IS NOT NULL AND [CeremonyId] IS NOT NULL AND [CeremonyBrowserDigest] IS NOT NULL AND [BrowserBudgetId] IS NOT NULL AND [UserId] IS NOT NULL AND [SecurityStamp] IS NOT NULL AND [AuthenticationGeneration] IS NOT NULL AND [AuthenticationGeneration] > 0 AND [FactorGeneration] IS NOT NULL AND [FactorGeneration] > 0 AND [EnrollmentRevision] IS NOT NULL AND [EnrollmentRevision] >= 0 AND [PasswordVerifiedAt] IS NOT NULL AND [PasswordVerifiedAt] >= [CreatedAt] AND [PasswordVerifiedAt] < [ExpiresAt] AND (([State] = 2 AND [MfaVerifiedAt] IS NULL AND [ConsentVerifiedAt] IS NULL) OR ([State] = 3 AND [MfaVerifiedAt] IS NOT NULL AND [MfaVerifiedAt] >= [PasswordVerifiedAt] AND [MfaVerifiedAt] < [ExpiresAt] AND [ConsentVerifiedAt] IS NULL) OR ([State] IN (4, 5, 8) AND [MfaVerifiedAt] IS NOT NULL AND [MfaVerifiedAt] >= [PasswordVerifiedAt] AND [MfaVerifiedAt] < [ExpiresAt] AND [ConsentVerifiedAt] IS NOT NULL AND [ConsentVerifiedAt] >= [MfaVerifiedAt] AND [ConsentVerifiedAt] < [ExpiresAt])))");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ManagedLoginInteraction_State",
                schema: "platform",
                table: "ManagedLoginInteraction",
                sql: "[State] IN (1, 2, 3, 4, 5, 6, 7, 8) AND ([State] <> 8 OR [DeviceEntryId] IS NOT NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DECLARE @rows bigint; SELECT @rows = COUNT_BIG(*) FROM [platform].[ManagedLoginInteraction] WITH (TABLOCKX, HOLDLOCK); SELECT @rows = COUNT_BIG(*) FROM [platform].[DevicePollingWindow] WITH (TABLOCKX, HOLDLOCK); IF EXISTS (SELECT 1 FROM [platform].[ManagedLoginInteraction] WHERE [DeviceEntryId] IS NOT NULL) OR EXISTS (SELECT 1 FROM [platform].[DevicePollingWindow] WHERE [NativeAuthorizationId] IS NOT NULL) THROW 51000, 'Device verification history must be retained', 1;");

            migrationBuilder.DropIndex(
                name: "IX_ManagedLoginInteraction_DeviceEntryId",
                schema: "platform",
                table: "ManagedLoginInteraction");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ManagedLoginInteraction_Code",
                schema: "platform",
                table: "ManagedLoginInteraction");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ManagedLoginInteraction_Native",
                schema: "platform",
                table: "ManagedLoginInteraction");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ManagedLoginInteraction_Proof",
                schema: "platform",
                table: "ManagedLoginInteraction");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ManagedLoginInteraction_State",
                schema: "platform",
                table: "ManagedLoginInteraction");

            migrationBuilder.DropColumn(
                name: "DeviceEntryId",
                schema: "platform",
                table: "ManagedLoginInteraction");

            migrationBuilder.DropColumn(
                name: "LastCleanupAttempt",
                schema: "platform",
                table: "DevicePollingWindow");

            migrationBuilder.DropColumn(
                name: "NativeAuthorizationId",
                schema: "platform",
                table: "DevicePollingWindow");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ManagedLoginInteraction_Code",
                schema: "platform",
                table: "ManagedLoginInteraction",
                sql: "([State] = 5 AND [AuthorizationId] IS NOT NULL AND [CodeId] IS NOT NULL) OR ([State] <> 5 AND [AuthorizationId] IS NULL AND [CodeId] IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ManagedLoginInteraction_Native",
                schema: "platform",
                table: "ManagedLoginInteraction",
                sql: "([RequestId] IS NULL AND [RequestExpiresAt] IS NULL AND [NativeDependencyCreatedAt] IS NULL AND [NativeDependencyExpiresAt] IS NULL) OR ([RequestId] IS NOT NULL AND [RequestExpiresAt] IS NOT NULL AND [NativeDependencyCreatedAt] IS NOT NULL AND [NativeDependencyExpiresAt] IS NOT NULL AND [RequestExpiresAt] >= [ExpiresAt] AND [NativeDependencyCreatedAt] >= [CreatedAt] AND [NativeDependencyExpiresAt] >= [RequestExpiresAt] AND [NativeDependencyExpiresAt] > [NativeDependencyCreatedAt] AND [RetainUntil] >= [NativeDependencyExpiresAt])");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ManagedLoginInteraction_Proof",
                schema: "platform",
                table: "ManagedLoginInteraction",
                sql: "[State] IN (6, 7) OR ([State] = 1 AND [CeremonyId] IS NULL AND [CeremonyBrowserDigest] IS NULL AND [BrowserBudgetId] IS NULL AND [UserId] IS NULL AND [SecurityStamp] IS NULL AND [AuthenticationGeneration] IS NULL AND [FactorGeneration] IS NULL AND [EnrollmentRevision] IS NULL AND [PasswordVerifiedAt] IS NULL AND [MfaVerifiedAt] IS NULL AND [ConsentVerifiedAt] IS NULL) OR ([RequestId] IS NOT NULL AND [CeremonyId] IS NOT NULL AND [CeremonyBrowserDigest] IS NOT NULL AND [BrowserBudgetId] IS NOT NULL AND [UserId] IS NOT NULL AND [SecurityStamp] IS NOT NULL AND [AuthenticationGeneration] IS NOT NULL AND [AuthenticationGeneration] > 0 AND [FactorGeneration] IS NOT NULL AND [FactorGeneration] > 0 AND [EnrollmentRevision] IS NOT NULL AND [EnrollmentRevision] >= 0 AND [PasswordVerifiedAt] IS NOT NULL AND [PasswordVerifiedAt] >= [CreatedAt] AND [PasswordVerifiedAt] < [ExpiresAt] AND (([State] = 2 AND [MfaVerifiedAt] IS NULL AND [ConsentVerifiedAt] IS NULL) OR ([State] = 3 AND [MfaVerifiedAt] IS NOT NULL AND [MfaVerifiedAt] >= [PasswordVerifiedAt] AND [MfaVerifiedAt] < [ExpiresAt] AND [ConsentVerifiedAt] IS NULL) OR ([State] IN (4, 5) AND [MfaVerifiedAt] IS NOT NULL AND [MfaVerifiedAt] >= [PasswordVerifiedAt] AND [MfaVerifiedAt] < [ExpiresAt] AND [ConsentVerifiedAt] IS NOT NULL AND [ConsentVerifiedAt] >= [MfaVerifiedAt] AND [ConsentVerifiedAt] < [ExpiresAt])))");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ManagedLoginInteraction_State",
                schema: "platform",
                table: "ManagedLoginInteraction",
                sql: "[State] IN (1, 2, 3, 4, 5, 6, 7)");
        }
    }
}
