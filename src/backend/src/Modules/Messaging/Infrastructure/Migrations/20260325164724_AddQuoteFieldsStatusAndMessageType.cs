using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Messaging.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddQuoteFieldsStatusAndMessageType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "status_color",
                table: "users",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "status_emoji",
                table: "users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "status_text",
                table: "users",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "message_type",
                table: "messages",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "text");

            migrationBuilder.AddColumn<string>(
                name: "quoted_author_display_name",
                table: "messages",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "quoted_content_snapshot",
                table: "messages",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "quoted_message_id",
                table: "messages",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "status_color",
                table: "users");

            migrationBuilder.DropColumn(
                name: "status_emoji",
                table: "users");

            migrationBuilder.DropColumn(
                name: "status_text",
                table: "users");

            migrationBuilder.DropColumn(
                name: "message_type",
                table: "messages");

            migrationBuilder.DropColumn(
                name: "quoted_author_display_name",
                table: "messages");

            migrationBuilder.DropColumn(
                name: "quoted_content_snapshot",
                table: "messages");

            migrationBuilder.DropColumn(
                name: "quoted_message_id",
                table: "messages");
        }
    }
}
