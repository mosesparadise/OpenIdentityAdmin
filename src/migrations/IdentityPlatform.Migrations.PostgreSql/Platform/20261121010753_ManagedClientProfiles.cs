using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IdentityPlatform.Migrations.PostgreSql.Platform
{
    /// <inheritdoc />
    public partial class ManagedClientProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ApplicationPolicy_Profile",
                schema: "platform",
                table: "ApplicationPolicy");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ApplicationPolicy_Revision",
                schema: "platform",
                table: "ApplicationPolicy");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ApplicationPolicy_Time",
                schema: "platform",
                table: "ApplicationPolicy");

            migrationBuilder.AlterColumn<string>(
                name: "Profile",
                schema: "platform",
                table: "ApplicationPolicy",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(14)",
                oldMaxLength: 14);

            migrationBuilder.AlterColumn<long>(
                name: "CredentialIssuedAt",
                schema: "platform",
                table: "ApplicationPolicy",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<long>(
                name: "CredentialExpiresAt",
                schema: "platform",
                table: "ApplicationPolicy",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ApplicationPolicy_Profile",
                schema: "platform",
                table: "ApplicationPolicy",
                sql: "RIGHT(\"Profile\", 1) = '1' AND (\"Profile\" IN ('managed-web-v1', 'managed-web-key-v1', 'managed-machine-v1', 'managed-machine-key-v1') OR \"Profile\" IN ('managed-native-v1', 'managed-browser-v1', 'managed-device-v1'))");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ApplicationPolicy_Revision",
                schema: "platform",
                table: "ApplicationPolicy",
                sql: "\"Revision\" >= 1 AND \"Revision\" >= \"CredentialRevision\" AND ((\"Profile\" IN ('managed-web-v1', 'managed-web-key-v1', 'managed-machine-v1', 'managed-machine-key-v1') AND \"CredentialRevision\" > 0) OR (\"Profile\" IN ('managed-native-v1', 'managed-browser-v1', 'managed-device-v1') AND \"CredentialRevision\" = 0))");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ApplicationPolicy_Time",
                schema: "platform",
                table: "ApplicationPolicy",
                sql: "\"ObservedAt\" >= 0 AND ((\"Profile\" IN ('managed-web-v1', 'managed-web-key-v1', 'managed-machine-v1', 'managed-machine-key-v1') AND \"CredentialIssuedAt\" IS NOT NULL AND \"CredentialExpiresAt\" IS NOT NULL AND \"CredentialIssuedAt\" >= 0 AND \"CredentialExpiresAt\" > \"CredentialIssuedAt\" AND \"ObservedAt\" >= \"CredentialIssuedAt\") OR (\"Profile\" IN ('managed-native-v1', 'managed-browser-v1', 'managed-device-v1') AND \"CredentialIssuedAt\" IS NULL AND \"CredentialExpiresAt\" IS NULL))");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $profile$ BEGIN
                    IF EXISTS (SELECT 1 FROM "platform"."ApplicationPolicy" WHERE "Profile" <> 'managed-web-v1') THEN
                        RAISE EXCEPTION 'Remove dependent managed client profiles before downgrading.';
                    END IF;
                END $profile$;
                """);

            migrationBuilder.DropCheckConstraint(
                name: "CK_ApplicationPolicy_Profile",
                schema: "platform",
                table: "ApplicationPolicy");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ApplicationPolicy_Revision",
                schema: "platform",
                table: "ApplicationPolicy");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ApplicationPolicy_Time",
                schema: "platform",
                table: "ApplicationPolicy");

            migrationBuilder.AlterColumn<string>(
                name: "Profile",
                schema: "platform",
                table: "ApplicationPolicy",
                type: "character varying(14)",
                maxLength: 14,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32);

            migrationBuilder.AlterColumn<long>(
                name: "CredentialIssuedAt",
                schema: "platform",
                table: "ApplicationPolicy",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "CredentialExpiresAt",
                schema: "platform",
                table: "ApplicationPolicy",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_ApplicationPolicy_Profile",
                schema: "platform",
                table: "ApplicationPolicy",
                sql: "\"Profile\" = 'managed-web-v1'");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ApplicationPolicy_Revision",
                schema: "platform",
                table: "ApplicationPolicy",
                sql: "\"Revision\" >= \"CredentialRevision\" AND \"CredentialRevision\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ApplicationPolicy_Time",
                schema: "platform",
                table: "ApplicationPolicy",
                sql: "\"CredentialIssuedAt\" >= 0 AND \"CredentialExpiresAt\" > \"CredentialIssuedAt\" AND \"ObservedAt\" >= \"CredentialIssuedAt\"");
        }
    }
}
