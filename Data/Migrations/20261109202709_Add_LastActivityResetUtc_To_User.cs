using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Studium_Users.Data.Migrations
{
    /// <inheritdoc />
    public partial class Add_LastActivityResetUtc_To_User : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastActivityResetUtc",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastActivityResetUtc",
                table: "Users");
        }
    }
}
