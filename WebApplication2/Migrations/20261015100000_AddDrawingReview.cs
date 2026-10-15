using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WebApplication2.Data;

#nullable disable

namespace WebApplication2.Migrations
{
    /// <summary>AI comparison of a project drawing with the SBC.</summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261015100000_AddDrawingReview")]
    public partial class AddDrawingReview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(name: "AiReview", table: "ProjectDocuments", type: "nvarchar(max)", nullable: true);
            migrationBuilder.AddColumn<long>(name: "AiReviewedAt", table: "ProjectDocuments", type: "bigint", nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "AiReview", table: "ProjectDocuments");
            migrationBuilder.DropColumn(name: "AiReviewedAt", table: "ProjectDocuments");
        }
    }
}
