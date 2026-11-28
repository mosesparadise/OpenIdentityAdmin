using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IdentityPlatform.Migrations.SqlServer.Platform
{
    /// <inheritdoc />
    public partial class ManagedKeyAuditRevision : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApplicationKeyAuditDetail",
                schema: "platform",
                columns: table => new
                {
                    AuditId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PreviousRevision = table.Column<long>(type: "bigint", nullable: false),
                    CredentialRevision = table.Column<long>(type: "bigint", nullable: false),
                    Fingerprint = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationKeyAuditDetail", x => x.AuditId);
                    table.CheckConstraint("CK_ApplicationKeyAuditDetail_Revision", "[PreviousRevision] >= 0 AND [CredentialRevision] > [PreviousRevision] AND [CredentialRevision] - [PreviousRevision] = 1");
                    table.ForeignKey(
                        name: "FK_ApplicationKeyAuditDetail_SecurityAuditIntent_AuditId",
                        column: x => x.AuditId,
                        principalSchema: "platform",
                        principalTable: "SecurityAuditIntent",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DECLARE @rows bigint; SELECT @rows = COUNT_BIG(*) FROM [platform].[ApplicationKeyAuditDetail] WITH (TABLOCKX, HOLDLOCK); IF @rows > 0 THROW 51000, 'Client key audit history must be retained', 1;");

            migrationBuilder.DropTable(
                name: "ApplicationKeyAuditDetail",
                schema: "platform");
        }
    }
}
