using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vocabula.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddChapterNewWords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "chapter_new_words",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    chapter_id = table.Column<Guid>(type: "uuid", nullable: false),
                    word = table.Column<string>(type: "text", nullable: false),
                    definition = table.Column<string>(type: "text", nullable: true),
                    position = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chapter_new_words", x => x.id);
                    table.ForeignKey(
                        name: "fk_chapter_new_words_story_chapters",
                        column: x => x.chapter_id,
                        principalTable: "story_chapters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_chapter_new_words_chapter_id",
                table: "chapter_new_words",
                column: "chapter_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "chapter_new_words");
        }
    }
}
