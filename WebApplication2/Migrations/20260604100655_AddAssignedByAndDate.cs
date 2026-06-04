using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebApplication2.Migrations
{
    /// <inheritdoc />
    public partial class AddAssignedByAndDate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "AssignedAt",
                table: "Bugs",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AssignedById",
                table: "Bugs",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AssignedByName",
                table: "Bugs",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AssignedAt",
                table: "Bugs");

            migrationBuilder.DropColumn(
                name: "AssignedById",
                table: "Bugs");

            migrationBuilder.DropColumn(
                name: "AssignedByName",
                table: "Bugs");
        }
    }
}
