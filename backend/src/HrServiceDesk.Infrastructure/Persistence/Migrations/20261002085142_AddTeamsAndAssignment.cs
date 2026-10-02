using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HrServiceDesk.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTeamsAndAssignment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "team_id",
                table: "tickets",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "tickets",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<Guid>(
                name: "responsible_team_id",
                table: "request_types",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "teams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    strategy = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    last_assigned_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_teams", x => x.id);
                    table.ForeignKey(
                        name: "fk_teams_users_last_assigned_user_id",
                        column: x => x.last_assigned_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "team_members",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    team_id = table.Column<Guid>(type: "uuid", nullable: false),
                    joined_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_team_members", x => new { x.team_id, x.user_id });
                    table.ForeignKey(
                        name: "fk_team_members_teams_team_id",
                        column: x => x.team_id,
                        principalTable: "teams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_team_members_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_tickets_team_id",
                table: "tickets",
                column: "team_id");

            migrationBuilder.CreateIndex(
                name: "ix_tickets_tenant_id_team_id",
                table: "tickets",
                columns: new[] { "tenant_id", "team_id" });

            migrationBuilder.CreateIndex(
                name: "ix_request_types_responsible_team_id",
                table: "request_types",
                column: "responsible_team_id");

            migrationBuilder.CreateIndex(
                name: "ix_team_members_user_id",
                table: "team_members",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_teams_last_assigned_user_id",
                table: "teams",
                column: "last_assigned_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_teams_tenant_id",
                table: "teams",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_teams_tenant_id_name",
                table: "teams",
                columns: new[] { "tenant_id", "name" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_request_types_teams_responsible_team_id",
                table: "request_types",
                column: "responsible_team_id",
                principalTable: "teams",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_tickets_teams_team_id",
                table: "tickets",
                column: "team_id",
                principalTable: "teams",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_request_types_teams_responsible_team_id",
                table: "request_types");

            migrationBuilder.DropForeignKey(
                name: "fk_tickets_teams_team_id",
                table: "tickets");

            migrationBuilder.DropTable(
                name: "team_members");

            migrationBuilder.DropTable(
                name: "teams");

            migrationBuilder.DropIndex(
                name: "ix_tickets_team_id",
                table: "tickets");

            migrationBuilder.DropIndex(
                name: "ix_tickets_tenant_id_team_id",
                table: "tickets");

            migrationBuilder.DropIndex(
                name: "ix_request_types_responsible_team_id",
                table: "request_types");

            migrationBuilder.DropColumn(
                name: "team_id",
                table: "tickets");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "tickets");

            migrationBuilder.DropColumn(
                name: "responsible_team_id",
                table: "request_types");
        }
    }
}
