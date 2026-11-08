using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Studium_Users.Migrations
{
    /// <inheritdoc />
    public partial class Initial_Migration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    FullName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Email = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    RegistrationNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    PhoneNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    NotifyReminders = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    NotifyConfirmations = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    IsBlocked = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    BlockReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    BlockedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NoShowCount30Days = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    TotalReservations30Days = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    CompletedReservations30Days = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    CancelledReservations30Days = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Action = table.Column<int>(type: "integer", nullable: false),
                    TimestampUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuditLogs_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Action",
                table: "AuditLogs",
                column: "Action");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_TimestampUtc",
                table: "AuditLogs",
                column: "TimestampUtc");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_UserId",
                table: "AuditLogs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_IsBlocked",
                table: "Users",
                column: "IsBlocked");

            migrationBuilder.CreateIndex(
                name: "IX_Users_RegistrationNumber",
                table: "Users",
                column: "RegistrationNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Role",
                table: "Users",
                column: "Role");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
