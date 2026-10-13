using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WebApplication2.Data;

#nullable disable

namespace WebApplication2.Migrations
{
    /// <summary>Adds the fix-review fields: evidence photo, rejection reason, submission time.</summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261013150000_AddBugFixReview")]
    public partial class AddBugFixReview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ResolutionEvidenceUrl",
                table: "Bugs",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewComment",
                table: "Bugs",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "SubmittedForReviewAt",
                table: "Bugs",
                type: "bigint",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "ResolutionEvidenceUrl", table: "Bugs");
            migrationBuilder.DropColumn(name: "ReviewComment", table: "Bugs");
            migrationBuilder.DropColumn(name: "SubmittedForReviewAt", table: "Bugs");
        }
    }
}
