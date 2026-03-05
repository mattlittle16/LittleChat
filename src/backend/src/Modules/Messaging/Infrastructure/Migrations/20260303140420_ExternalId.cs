using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Messaging.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ExternalId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // external_id column already exists in the Initial migration
            migrationBuilder.CreateIndex(
                name: "IX_users_external_id",
                table: "users",
                column: "external_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_users_external_id",
                table: "users");

            migrationBuilder.DropColumn(
                name: "external_id",
                table: "users");
        }
    }
}
