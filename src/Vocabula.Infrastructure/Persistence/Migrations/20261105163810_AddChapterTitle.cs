using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vocabula.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddChapterTitle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "title",
                table: "story_chapters",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "title",
                table: "story_chapters");
        }
    }
}
