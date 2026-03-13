using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Messaging.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserProfileFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<float>(
                name: "crop_x",
                table: "users",
                type: "real",
                nullable: true);

            migrationBuilder.AddColumn<float>(
                name: "crop_y",
                table: "users",
                type: "real",
                nullable: true);

            migrationBuilder.AddColumn<float>(
                name: "crop_zoom",
                table: "users",
                type: "real",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "profile_image_path",
                table: "users",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "crop_x",
                table: "users");

            migrationBuilder.DropColumn(
                name: "crop_y",
                table: "users");

            migrationBuilder.DropColumn(
                name: "crop_zoom",
                table: "users");

            migrationBuilder.DropColumn(
                name: "profile_image_path",
                table: "users");
        }
    }
}
