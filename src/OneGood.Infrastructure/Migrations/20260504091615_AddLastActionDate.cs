using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneGood.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLastActionDate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastActionDate",
                table: "UserProfiles",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastActionDate",
                table: "UserProfiles");
        }
    }
}
