using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vocabula.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddChapterNewWordKind : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "kind",
                table: "chapter_new_words",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "kind",
                table: "chapter_new_words");
        }
    }
}
