using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EnrichedMessaging.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialEnrichedMessaging : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "bookmark_folders",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bookmark_folders", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "link_previews",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    message_id = table.Column<Guid>(type: "uuid", nullable: false),
                    url = table.Column<string>(type: "text", nullable: false),
                    title = table.Column<string>(type: "text", nullable: true),
                    description = table.Column<string>(type: "text", nullable: true),
                    thumbnail_url = table.Column<string>(type: "text", nullable: true),
                    is_dismissed = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    fetched_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_link_previews", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "message_highlights",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    room_id = table.Column<Guid>(type: "uuid", nullable: false),
                    message_id = table.Column<Guid>(type: "uuid", nullable: false),
                    highlighted_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    highlighted_by_display_name = table.Column<string>(type: "text", nullable: false),
                    highlighted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_message_highlights", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "polls",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    message_id = table.Column<Guid>(type: "uuid", nullable: false),
                    question = table.Column<string>(type: "text", nullable: false),
                    vote_mode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_polls", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "message_bookmarks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    message_id = table.Column<Guid>(type: "uuid", nullable: false),
                    folder_id = table.Column<Guid>(type: "uuid", nullable: true),
                    room_id = table.Column<Guid>(type: "uuid", nullable: false),
                    room_name = table.Column<string>(type: "text", nullable: false),
                    author_display_name = table.Column<string>(type: "text", nullable: false),
                    content_preview = table.Column<string>(type: "text", nullable: false),
                    message_created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_message_bookmarks", x => x.id);
                    table.ForeignKey(
                        name: "FK_message_bookmarks_bookmark_folders_folder_id",
                        column: x => x.folder_id,
                        principalTable: "bookmark_folders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "poll_options",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    poll_id = table.Column<Guid>(type: "uuid", nullable: false),
                    text = table.Column<string>(type: "text", nullable: false),
                    display_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_poll_options", x => x.id);
                    table.ForeignKey(
                        name: "FK_poll_options_polls_poll_id",
                        column: x => x.poll_id,
                        principalTable: "polls",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "poll_votes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    poll_id = table.Column<Guid>(type: "uuid", nullable: false),
                    option_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    display_name = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_poll_votes", x => x.id);
                    table.ForeignKey(
                        name: "FK_poll_votes_poll_options_option_id",
                        column: x => x.option_id,
                        principalTable: "poll_options",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "link_previews_message_id_idx",
                table: "link_previews",
                column: "message_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_message_bookmarks_folder_id",
                table: "message_bookmarks",
                column: "folder_id");

            migrationBuilder.CreateIndex(
                name: "message_bookmarks_user_message_idx",
                table: "message_bookmarks",
                columns: new[] { "user_id", "message_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "message_highlights_room_message_idx",
                table: "message_highlights",
                columns: new[] { "room_id", "message_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_poll_options_poll_id",
                table: "poll_options",
                column: "poll_id");

            migrationBuilder.CreateIndex(
                name: "IX_poll_votes_option_id",
                table: "poll_votes",
                column: "option_id");

            migrationBuilder.CreateIndex(
                name: "poll_votes_unique_idx",
                table: "poll_votes",
                columns: new[] { "poll_id", "option_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "polls_message_id_idx",
                table: "polls",
                column: "message_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "link_previews");

            migrationBuilder.DropTable(
                name: "message_bookmarks");

            migrationBuilder.DropTable(
                name: "message_highlights");

            migrationBuilder.DropTable(
                name: "poll_votes");

            migrationBuilder.DropTable(
                name: "bookmark_folders");

            migrationBuilder.DropTable(
                name: "poll_options");

            migrationBuilder.DropTable(
                name: "polls");
        }
    }
}
