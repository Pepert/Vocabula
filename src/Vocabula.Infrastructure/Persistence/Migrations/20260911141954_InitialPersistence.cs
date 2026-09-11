using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vocabula.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialPersistence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "app_users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    normalized_email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_app_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "learning_profiles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    language_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_learning_profiles", x => x.id);
                    table.ForeignKey(
                        name: "fk_learning_profiles_app_users",
                        column: x => x.user_id,
                        principalTable: "app_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "grammar_points",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    learning_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    explanation = table.Column<string>(type: "text", nullable: false),
                    first_seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_used_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    times_used = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_grammar_points", x => x.id);
                    table.ForeignKey(
                        name: "fk_grammar_points_learning_profiles",
                        column: x => x.learning_profile_id,
                        principalTable: "learning_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "learning_sources",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    learning_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    processing_status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_learning_sources", x => x.id);
                    table.ForeignKey(
                        name: "fk_learning_sources_learning_profiles",
                        column: x => x.learning_profile_id,
                        principalTable: "learning_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "stories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    learning_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    current_summary = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stories", x => x.id);
                    table.ForeignKey(
                        name: "fk_stories_learning_profiles",
                        column: x => x.learning_profile_id,
                        principalTable: "learning_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "vocabulary_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    learning_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    term = table.Column<string>(type: "text", nullable: false),
                    normalized_term = table.Column<string>(type: "text", nullable: false),
                    translation = table.Column<string>(type: "text", nullable: true),
                    first_seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_used_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    times_used = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vocabulary_items", x => x.id);
                    table.ForeignKey(
                        name: "fk_vocabulary_items_learning_profiles",
                        column: x => x.learning_profile_id,
                        principalTable: "learning_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "story_chapters",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    story_id = table.Column<Guid>(type: "uuid", nullable: false),
                    chapter_number = table.Column<int>(type: "integer", nullable: false),
                    content = table.Column<string>(type: "text", nullable: false),
                    resulting_summary = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_story_chapters", x => x.id);
                    table.CheckConstraint("ck_story_chapters_chapter_number_gt_0", "\"chapter_number\" > 0");
                    table.ForeignKey(
                        name: "fk_story_chapters_stories",
                        column: x => x.story_id,
                        principalTable: "stories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_app_users_normalized_email",
                table: "app_users",
                column: "normalized_email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_grammar_points_learning_profile_id",
                table: "grammar_points",
                column: "learning_profile_id");

            migrationBuilder.CreateIndex(
                name: "ix_learning_profiles_user_id_language_code",
                table: "learning_profiles",
                columns: new[] { "user_id", "language_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_learning_sources_learning_profile_id",
                table: "learning_sources",
                column: "learning_profile_id");

            migrationBuilder.CreateIndex(
                name: "IX_stories_learning_profile_id",
                table: "stories",
                column: "learning_profile_id");

            migrationBuilder.CreateIndex(
                name: "ix_story_chapters_story_id_chapter_number",
                table: "story_chapters",
                columns: new[] { "story_id", "chapter_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_vocabulary_items_learning_profile_id_first_seen_at",
                table: "vocabulary_items",
                columns: new[] { "learning_profile_id", "first_seen_at" });

            migrationBuilder.CreateIndex(
                name: "ix_vocabulary_items_learning_profile_id_last_used_at",
                table: "vocabulary_items",
                columns: new[] { "learning_profile_id", "last_used_at" });

            migrationBuilder.CreateIndex(
                name: "ix_vocabulary_items_learning_profile_id_normalized_term",
                table: "vocabulary_items",
                columns: new[] { "learning_profile_id", "normalized_term" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "grammar_points");

            migrationBuilder.DropTable(
                name: "learning_sources");

            migrationBuilder.DropTable(
                name: "story_chapters");

            migrationBuilder.DropTable(
                name: "vocabulary_items");

            migrationBuilder.DropTable(
                name: "stories");

            migrationBuilder.DropTable(
                name: "learning_profiles");

            migrationBuilder.DropTable(
                name: "app_users");
        }
    }
}
