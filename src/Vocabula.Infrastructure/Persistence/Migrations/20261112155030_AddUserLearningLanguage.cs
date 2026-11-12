using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vocabula.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUserLearningLanguage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Null = the user has not chosen a language yet: the app prompts
            // on first use, and every learner starts by picking one.
            migrationBuilder.AddColumn<string>(
                name: "learning_language",
                table: "app_users",
                type: "character varying(5)",
                maxLength: 5,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "learning_language",
                table: "app_users");
        }
    }
}
