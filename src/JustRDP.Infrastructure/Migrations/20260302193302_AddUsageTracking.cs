using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JustRDP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUsageTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ConnectCount",
                table: "TreeEntries",
                type: "INTEGER",
                nullable: true,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastConnectedAt",
                table: "TreeEntries",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConnectCount",
                table: "TreeEntries");

            migrationBuilder.DropColumn(
                name: "LastConnectedAt",
                table: "TreeEntries");
        }
    }
}
