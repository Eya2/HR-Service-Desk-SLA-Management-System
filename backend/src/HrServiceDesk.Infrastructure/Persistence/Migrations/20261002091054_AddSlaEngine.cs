using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HrServiceDesk.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSlaEngine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "first_responded_at",
                table: "tickets",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "first_response_breached",
                table: "tickets",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "first_response_due_at",
                table: "tickets",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "first_response_target_minutes",
                table: "tickets",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "resolution_breached",
                table: "tickets",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "resolution_due_at",
                table: "tickets",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "resolution_target_minutes",
                table: "tickets",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "sla_at_risk_percent",
                table: "tickets",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string[]>(
                name: "sla_pause_statuses",
                table: "tickets",
                type: "character varying(32)[]",
                nullable: false,
                defaultValue: new string[0]);

            migrationBuilder.AddColumn<string>(
                name: "sla_pauses",
                table: "tickets",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "sla_policy_id",
                table: "tickets",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "sla_started_at",
                table: "tickets",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "sla_state",
                table: "tickets",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "sla_stopped_at",
                table: "tickets",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "sla_policy_id",
                table: "request_types",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "business_calendars",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    time_zone_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    working_hours = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_business_calendars", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sla_policies",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    is_default = table.Column<bool>(type: "boolean", nullable: false),
                    at_risk_threshold_percent = table.Column<int>(type: "integer", nullable: false),
                    pause_statuses = table.Column<string[]>(type: "character varying(32)[]", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    targets = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sla_policies", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "holidays",
                columns: table => new
                {
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    calendar_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_holidays", x => new { x.calendar_id, x.date });
                    table.ForeignKey(
                        name: "fk_holidays_business_calendars_calendar_id",
                        column: x => x.calendar_id,
                        principalTable: "business_calendars",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_tickets_sla_state_resolution_due_at",
                table: "tickets",
                columns: new[] { "sla_state", "resolution_due_at" });

            migrationBuilder.CreateIndex(
                name: "ix_request_types_sla_policy_id",
                table: "request_types",
                column: "sla_policy_id");

            migrationBuilder.CreateIndex(
                name: "ix_business_calendars_tenant_id",
                table: "business_calendars",
                column: "tenant_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sla_policies_tenant_id",
                table: "sla_policies",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_sla_policies_tenant_id_name",
                table: "sla_policies",
                columns: new[] { "tenant_id", "name" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_request_types_sla_policies_sla_policy_id",
                table: "request_types",
                column: "sla_policy_id",
                principalTable: "sla_policies",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_request_types_sla_policies_sla_policy_id",
                table: "request_types");

            migrationBuilder.DropTable(
                name: "holidays");

            migrationBuilder.DropTable(
                name: "sla_policies");

            migrationBuilder.DropTable(
                name: "business_calendars");

            migrationBuilder.DropIndex(
                name: "ix_tickets_sla_state_resolution_due_at",
                table: "tickets");

            migrationBuilder.DropIndex(
                name: "ix_request_types_sla_policy_id",
                table: "request_types");

            migrationBuilder.DropColumn(
                name: "first_responded_at",
                table: "tickets");

            migrationBuilder.DropColumn(
                name: "first_response_breached",
                table: "tickets");

            migrationBuilder.DropColumn(
                name: "first_response_due_at",
                table: "tickets");

            migrationBuilder.DropColumn(
                name: "first_response_target_minutes",
                table: "tickets");

            migrationBuilder.DropColumn(
                name: "resolution_breached",
                table: "tickets");

            migrationBuilder.DropColumn(
                name: "resolution_due_at",
                table: "tickets");

            migrationBuilder.DropColumn(
                name: "resolution_target_minutes",
                table: "tickets");

            migrationBuilder.DropColumn(
                name: "sla_at_risk_percent",
                table: "tickets");

            migrationBuilder.DropColumn(
                name: "sla_pause_statuses",
                table: "tickets");

            migrationBuilder.DropColumn(
                name: "sla_pauses",
                table: "tickets");

            migrationBuilder.DropColumn(
                name: "sla_policy_id",
                table: "tickets");

            migrationBuilder.DropColumn(
                name: "sla_started_at",
                table: "tickets");

            migrationBuilder.DropColumn(
                name: "sla_state",
                table: "tickets");

            migrationBuilder.DropColumn(
                name: "sla_stopped_at",
                table: "tickets");

            migrationBuilder.DropColumn(
                name: "sla_policy_id",
                table: "request_types");
        }
    }
}
