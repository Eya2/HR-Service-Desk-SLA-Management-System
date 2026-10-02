using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HrServiceDesk.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationsAndEscalation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "escalation_rules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    trigger = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    no_response_hours = table.Column<int>(type: "integer", nullable: true),
                    action = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    target_team_id = table.Column<Guid>(type: "uuid", nullable: true),
                    request_type_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_escalation_rules", x => x.id);
                    table.ForeignKey(
                        name: "fk_escalation_rules_request_types_request_type_id",
                        column: x => x.request_type_id,
                        principalTable: "request_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_escalation_rules_teams_target_team_id",
                        column: x => x.target_team_id,
                        principalTable: "teams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "notifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ticket_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    read_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notifications", x => x.id);
                    table.ForeignKey(
                        name: "fk_notifications_tickets_ticket_id",
                        column: x => x.ticket_id,
                        principalTable: "tickets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_notifications_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "escalation_executions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ticket_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rule_id = table.Column<Guid>(type: "uuid", nullable: false),
                    executed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_escalation_executions", x => x.id);
                    table.ForeignKey(
                        name: "fk_escalation_executions_escalation_rules_rule_id",
                        column: x => x.rule_id,
                        principalTable: "escalation_rules",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_escalation_executions_tickets_ticket_id",
                        column: x => x.ticket_id,
                        principalTable: "tickets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_escalation_executions_rule_id",
                table: "escalation_executions",
                column: "rule_id");

            migrationBuilder.CreateIndex(
                name: "ix_escalation_executions_tenant_id",
                table: "escalation_executions",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_escalation_executions_ticket_id_rule_id",
                table: "escalation_executions",
                columns: new[] { "ticket_id", "rule_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_escalation_rules_request_type_id",
                table: "escalation_rules",
                column: "request_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_escalation_rules_target_team_id",
                table: "escalation_rules",
                column: "target_team_id");

            migrationBuilder.CreateIndex(
                name: "ix_escalation_rules_tenant_id",
                table: "escalation_rules",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_notifications_tenant_id",
                table: "notifications",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_notifications_ticket_id",
                table: "notifications",
                column: "ticket_id");

            migrationBuilder.CreateIndex(
                name: "ix_notifications_user_id_read_at_created_at",
                table: "notifications",
                columns: new[] { "user_id", "read_at", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "escalation_executions");

            migrationBuilder.DropTable(
                name: "notifications");

            migrationBuilder.DropTable(
                name: "escalation_rules");
        }
    }
}
