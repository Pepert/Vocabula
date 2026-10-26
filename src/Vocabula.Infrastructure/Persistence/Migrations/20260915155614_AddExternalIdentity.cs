using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vocabula.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddExternalIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "normalized_email",
                table: "app_users",
                type: "character varying(320)",
                maxLength: 320,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(320)",
                oldMaxLength: 320);

            migrationBuilder.AlterColumn<string>(
                name: "email",
                table: "app_users",
                type: "character varying(320)",
                maxLength: 320,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(320)",
                oldMaxLength: 320);

            migrationBuilder.AddColumn<string>(
                name: "external_subject",
                table: "app_users",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "identity_provider",
                table: "app_users",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_app_users_identity_provider_external_subject",
                table: "app_users",
                columns: new[] { "identity_provider", "external_subject" },
                unique: true,
                filter: "identity_provider IS NOT NULL AND external_subject IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_app_users_identity_provider_external_subject",
                table: "app_users");

            migrationBuilder.DropColumn(
                name: "external_subject",
                table: "app_users");

            migrationBuilder.DropColumn(
                name: "identity_provider",
                table: "app_users");

            migrationBuilder.AlterColumn<string>(
                name: "normalized_email",
                table: "app_users",
                type: "character varying(320)",
                maxLength: 320,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(320)",
                oldMaxLength: 320,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "email",
                table: "app_users",
                type: "character varying(320)",
                maxLength: 320,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(320)",
                oldMaxLength: 320,
                oldNullable: true);
        }
    }
}
