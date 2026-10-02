using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace HrServiceDesk.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddApprovalWorkflows : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ticket_approvals",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ticket_id = table.Column<Guid>(type: "uuid", nullable: false),
                    step_order = table.Column<int>(type: "integer", nullable: false),
                    step_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    approver_role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    approver_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    decision = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    decided_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    decided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    comment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ticket_approvals", x => x.id);
                    table.ForeignKey(
                        name: "fk_ticket_approvals_tickets_ticket_id",
                        column: x => x.ticket_id,
                        principalTable: "tickets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_ticket_approvals_users_approver_user_id",
                        column: x => x.approver_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_ticket_approvals_users_decided_by_id",
                        column: x => x.decided_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "workflow_definitions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_workflow_definitions", x => x.id);
                    table.ForeignKey(
                        name: "fk_workflow_definitions_request_types_request_type_id",
                        column: x => x.request_type_id,
                        principalTable: "request_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "workflow_steps",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    order = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    approver_role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    workflow_definition_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_workflow_steps", x => x.id);
                    table.ForeignKey(
                        name: "fk_workflow_steps_workflow_definitions_workflow_definition_id",
                        column: x => x.workflow_definition_id,
                        principalTable: "workflow_definitions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_ticket_approvals_approver_user_id_decision",
                table: "ticket_approvals",
                columns: new[] { "approver_user_id", "decision" });

            migrationBuilder.CreateIndex(
                name: "ix_ticket_approvals_decided_by_id",
                table: "ticket_approvals",
                column: "decided_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_ticket_approvals_tenant_id",
                table: "ticket_approvals",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_ticket_approvals_ticket_id_step_order",
                table: "ticket_approvals",
                columns: new[] { "ticket_id", "step_order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_workflow_definitions_request_type_id",
                table: "workflow_definitions",
                column: "request_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_workflow_definitions_tenant_id",
                table: "workflow_definitions",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_workflow_definitions_tenant_id_request_type_id",
                table: "workflow_definitions",
                columns: new[] { "tenant_id", "request_type_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_workflow_steps_workflow_definition_id",
                table: "workflow_steps",
                column: "workflow_definition_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ticket_approvals");

            migrationBuilder.DropTable(
                name: "workflow_steps");

            migrationBuilder.DropTable(
                name: "workflow_definitions");
        }
    }
}
