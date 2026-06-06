using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebApplication2.Migrations
{
    /// <inheritdoc />
    public partial class testCase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TestCaseId",
                table: "Bugs",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TestCases",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Steps = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ExpectedResult = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProjectId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestCases", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Bugs_TestCaseId",
                table: "Bugs",
                column: "TestCaseId");

            migrationBuilder.AddForeignKey(
                name: "FK_Bugs_TestCases_TestCaseId",
                table: "Bugs",
                column: "TestCaseId",
                principalTable: "TestCases",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Bugs_TestCases_TestCaseId",
                table: "Bugs");

            migrationBuilder.DropTable(
                name: "TestCases");

            migrationBuilder.DropIndex(
                name: "IX_Bugs_TestCaseId",
                table: "Bugs");

            migrationBuilder.DropColumn(
                name: "TestCaseId",
                table: "Bugs");
        }
    }
}
