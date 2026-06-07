using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebApplication2.Migrations
{
    /// <inheritdoc />
    public partial class testcases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_TestCases_ProjectId",
                table: "TestCases",
                column: "ProjectId");

            migrationBuilder.AddForeignKey(
                name: "FK_TestCases_Projects_ProjectId",
                table: "TestCases",
                column: "ProjectId",
                principalTable: "Projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TestCases_Projects_ProjectId",
                table: "TestCases");

            migrationBuilder.DropIndex(
                name: "IX_TestCases_ProjectId",
                table: "TestCases");
        }
    }
}
