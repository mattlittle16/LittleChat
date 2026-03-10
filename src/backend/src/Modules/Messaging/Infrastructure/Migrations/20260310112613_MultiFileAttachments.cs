using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Messaging.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MultiFileAttachments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Create the new table first so we can migrate data into it
            migrationBuilder.CreateTable(
                name: "message_attachments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    message_id = table.Column<Guid>(type: "uuid", nullable: false),
                    file_name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    file_size = table.Column<long>(type: "bigint", nullable: false),
                    file_path = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    content_type = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    is_image = table.Column<bool>(type: "boolean", nullable: false),
                    display_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_message_attachments", x => x.id);
                    table.ForeignKey(
                        name: "FK_message_attachments_messages_message_id",
                        column: x => x.message_id,
                        principalTable: "messages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_message_attachments_message_id",
                table: "message_attachments",
                column: "message_id");

            // Migrate existing single-file attachments into the new table
            migrationBuilder.Sql(@"
                INSERT INTO message_attachments (id, message_id, file_name, file_size, file_path, content_type, is_image, display_order)
                SELECT
                    gen_random_uuid(),
                    id,
                    COALESCE(file_name, ''),
                    COALESCE(file_size, 0),
                    COALESCE(file_path, ''),
                    CASE
                        WHEN file_name ILIKE '%.jpg'  THEN 'image/jpeg'
                        WHEN file_name ILIKE '%.jpeg' THEN 'image/jpeg'
                        WHEN file_name ILIKE '%.png'  THEN 'image/png'
                        WHEN file_name ILIKE '%.gif'  THEN 'image/gif'
                        WHEN file_name ILIKE '%.webp' THEN 'image/webp'
                        WHEN file_name ILIKE '%.bmp'  THEN 'image/bmp'
                        WHEN file_name ILIKE '%.svg'  THEN 'image/svg+xml'
                        ELSE 'application/octet-stream'
                    END,
                    file_name ILIKE ANY(ARRAY['%.jpg','%.jpeg','%.png','%.gif','%.webp','%.bmp','%.svg']),
                    0
                FROM messages
                WHERE file_path IS NOT NULL AND file_path <> '';
            ");

            migrationBuilder.DropColumn(
                name: "file_name",
                table: "messages");

            migrationBuilder.DropColumn(
                name: "file_path",
                table: "messages");

            migrationBuilder.DropColumn(
                name: "file_size",
                table: "messages");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "message_attachments");

            migrationBuilder.AddColumn<string>(
                name: "file_name",
                table: "messages",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "file_path",
                table: "messages",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "file_size",
                table: "messages",
                type: "bigint",
                nullable: true);
        }
    }
}
