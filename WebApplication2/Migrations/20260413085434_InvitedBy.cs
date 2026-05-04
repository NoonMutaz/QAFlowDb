using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebApplication2.Migrations
{
    /// <inheritdoc />
    public partial class InvitedBy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "InvitedById",
                table: "ProjectMembers",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectMembers_InvitedById",
                table: "ProjectMembers",
                column: "InvitedById");

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectMembers_Users_InvitedById",
                table: "ProjectMembers",
                column: "InvitedById",
                principalTable: "Users",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProjectMembers_Users_InvitedById",
                table: "ProjectMembers");

            migrationBuilder.DropIndex(
                name: "IX_ProjectMembers_InvitedById",
                table: "ProjectMembers");

            migrationBuilder.DropColumn(
                name: "InvitedById",
                table: "ProjectMembers");
        }
    }
}
