using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WebApplication2.Data;

#nullable disable

namespace WebApplication2.Migrations
{
    /// <summary>Assignment deadline, fix approver, and phase closure.</summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261014100000_AddDeadlineAndPhaseClosure")]
    public partial class AddDeadlineAndPhaseClosure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(name: "Deadline", table: "Bugs", type: "nvarchar(max)", nullable: true);
            migrationBuilder.AddColumn<string>(name: "ReviewedByName", table: "Bugs", type: "nvarchar(max)", nullable: true);
            migrationBuilder.AddColumn<long>(name: "ReviewedAt", table: "Bugs", type: "bigint", nullable: true);
            migrationBuilder.AddColumn<long>(name: "ClosedAt", table: "ProjectPhases", type: "bigint", nullable: true);
            migrationBuilder.AddColumn<string>(name: "ClosedByName", table: "ProjectPhases", type: "nvarchar(max)", nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "Deadline", table: "Bugs");
            migrationBuilder.DropColumn(name: "ReviewedByName", table: "Bugs");
            migrationBuilder.DropColumn(name: "ReviewedAt", table: "Bugs");
            migrationBuilder.DropColumn(name: "ClosedAt", table: "ProjectPhases");
            migrationBuilder.DropColumn(name: "ClosedByName", table: "ProjectPhases");
        }
    }
}
