using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Studium_Users.Data.Migrations
{
    /// <inheritdoc />
    public partial class Add_InboxMessages_And_BlockedUntilUtc : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "BlockedUntilUtc",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "InboxMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HandlerName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    ReceivedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InboxMessages", x => new { x.Id, x.HandlerName });
                });

            migrationBuilder.CreateIndex(
                name: "IX_InboxMessages_ReceivedAtUtc",
                table: "InboxMessages",
                column: "ReceivedAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InboxMessages");

            migrationBuilder.DropColumn(
                name: "BlockedUntilUtc",
                table: "Users");
        }
    }
}
