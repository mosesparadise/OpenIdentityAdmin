using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IdentityPlatform.Migrations.SqlServer.Platform
{
    /// <inheritdoc />
    public partial class ApplicationGrantRevocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApplicationGrantJob",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApplicationId = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false, collation: "Latin1_General_100_BIN2"),
                    ApplicationRevision = table.Column<long>(type: "bigint", nullable: false),
                    EffectiveApprovalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    State = table.Column<int>(type: "int", nullable: false),
                    Failure = table.Column<int>(type: "int", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    AuthorizationCount = table.Column<long>(type: "bigint", nullable: false),
                    TokenCount = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<long>(type: "bigint", nullable: false),
                    ObservedAt = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationGrantJob", x => x.Id);
                    table.CheckConstraint("CK_ApplicationGrantJob_Count", "[AuthorizationCount] >= 0 AND [TokenCount] >= 0");
                    table.CheckConstraint("CK_ApplicationGrantJob_Revision", "[Revision] > 0 AND [ApplicationRevision] > 0");
                    table.CheckConstraint("CK_ApplicationGrantJob_State", "[State] IN (1, 2, 3, 4) AND [Failure] IN (0, 1, 2, 3, 4) AND (([State] = 3 AND [Failure] <> 0) OR ([State] <> 3 AND [Failure] = 0))");
                    table.CheckConstraint("CK_ApplicationGrantJob_Time", "[CreatedAt] >= 0 AND [ObservedAt] >= [CreatedAt]");
                    table.ForeignKey(
                        name: "FK_ApplicationGrantJob_ApplicationPolicy_ApplicationId",
                        column: x => x.ApplicationId,
                        principalSchema: "platform",
                        principalTable: "ApplicationPolicy",
                        principalColumn: "ApplicationId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ApplicationGrantApproval",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JobId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false, collation: "Latin1_General_100_BIN2"),
                    ApplicationId = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false, collation: "Latin1_General_100_BIN2"),
                    Action = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<int>(type: "int", nullable: false),
                    RequestKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ScopedKeyHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false, collation: "Latin1_General_100_BIN2"),
                    RequestHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false, collation: "Latin1_General_100_BIN2"),
                    ApplicationRevision = table.Column<long>(type: "bigint", nullable: false),
                    ApprovedAt = table.Column<long>(type: "bigint", nullable: false),
                    FactorVerifiedAt = table.Column<long>(type: "bigint", nullable: false),
                    ReplayUntil = table.Column<long>(type: "bigint", nullable: false),
                    AuditId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationGrantApproval", x => x.Id);
                    table.CheckConstraint("CK_ApplicationGrantApproval_Intent", "[Action] IN (1, 2) AND [Reason] IN (1, 2, 3) AND [ApplicationRevision] > 0");
                    table.CheckConstraint("CK_ApplicationGrantApproval_Time", "[FactorVerifiedAt] >= 0 AND [ApprovedAt] >= [FactorVerifiedAt] AND [ReplayUntil] > [ApprovedAt]");
                    table.ForeignKey(
                        name: "FK_ApplicationGrantApproval_ApplicationGrantJob_JobId",
                        column: x => x.JobId,
                        principalSchema: "platform",
                        principalTable: "ApplicationGrantJob",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ApplicationGrantApproval_SecurityAuditIntent_AuditId",
                        column: x => x.AuditId,
                        principalSchema: "platform",
                        principalTable: "SecurityAuditIntent",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ApplicationGrantAudit",
                schema: "platform",
                columns: table => new
                {
                    AuditId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JobId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApprovalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JobRevision = table.Column<long>(type: "bigint", nullable: false),
                    AuthorizationDelta = table.Column<long>(type: "bigint", nullable: false),
                    TokenDelta = table.Column<long>(type: "bigint", nullable: false),
                    AuthorizationCount = table.Column<long>(type: "bigint", nullable: false),
                    TokenCount = table.Column<long>(type: "bigint", nullable: false),
                    State = table.Column<int>(type: "int", nullable: false),
                    Failure = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationGrantAudit", x => x.AuditId);
                    table.CheckConstraint("CK_ApplicationGrantAudit_Count", "[AuthorizationCount] >= 0 AND [TokenCount] >= 0");
                    table.CheckConstraint("CK_ApplicationGrantAudit_Delta", "[JobRevision] > 0 AND [AuthorizationDelta] >= 0 AND [TokenDelta] >= 0 AND [AuthorizationDelta] <= [AuthorizationCount] AND [TokenDelta] <= [TokenCount]");
                    table.CheckConstraint("CK_ApplicationGrantAudit_State", "[State] IN (1, 2, 3, 4) AND [Failure] IN (0, 1, 2, 3, 4) AND (([State] = 3 AND [Failure] <> 0) OR ([State] <> 3 AND [Failure] = 0))");
                    table.ForeignKey(
                        name: "FK_ApplicationGrantAudit_ApplicationGrantApproval_ApprovalId",
                        column: x => x.ApprovalId,
                        principalSchema: "platform",
                        principalTable: "ApplicationGrantApproval",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ApplicationGrantAudit_ApplicationGrantJob_JobId",
                        column: x => x.JobId,
                        principalSchema: "platform",
                        principalTable: "ApplicationGrantJob",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ApplicationGrantAudit_SecurityAuditIntent_AuditId",
                        column: x => x.AuditId,
                        principalSchema: "platform",
                        principalTable: "SecurityAuditIntent",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationGrantApproval_AuditId",
                schema: "platform",
                table: "ApplicationGrantApproval",
                column: "AuditId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationGrantApproval_JobId",
                schema: "platform",
                table: "ApplicationGrantApproval",
                column: "JobId");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationGrantApproval_ScopedKeyHash",
                schema: "platform",
                table: "ApplicationGrantApproval",
                column: "ScopedKeyHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationGrantAudit_ApprovalId",
                schema: "platform",
                table: "ApplicationGrantAudit",
                column: "ApprovalId");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationGrantAudit_JobId_JobRevision",
                schema: "platform",
                table: "ApplicationGrantAudit",
                columns: new[] { "JobId", "JobRevision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationGrantJob_ApplicationId_State",
                schema: "platform",
                table: "ApplicationGrantJob",
                columns: new[] { "ApplicationId", "State" });

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationGrantJob_State_ObservedAt_Id",
                schema: "platform",
                table: "ApplicationGrantJob",
                columns: new[] { "State", "ObservedAt", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApplicationGrantAudit",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "ApplicationGrantApproval",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "ApplicationGrantJob",
                schema: "platform");
        }
    }
}
