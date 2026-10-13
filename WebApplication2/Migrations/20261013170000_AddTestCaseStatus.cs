using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WebApplication2.Data;

#nullable disable

namespace WebApplication2.Migrations
{
    /// <summary>Stores the Pass/Fail result of a project SBC clause.</summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261013170000_AddTestCaseStatus")]
    public partial class AddTestCaseStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "TestCases",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "Status", table: "TestCases");
        }
    }
}
