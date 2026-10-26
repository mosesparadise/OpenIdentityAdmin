using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IdentityPlatform.Migrations.SqlServer.Sessions
{
    /// <inheritdoc />
    public partial class BrowserAuditCorrelation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "RelatedCorrelationId",
                schema: "bff",
                table: "BrowserSessionAudit",
                type: "uniqueidentifier",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RelatedCorrelationId",
                schema: "bff",
                table: "BrowserSessionAudit");
        }
    }
}
