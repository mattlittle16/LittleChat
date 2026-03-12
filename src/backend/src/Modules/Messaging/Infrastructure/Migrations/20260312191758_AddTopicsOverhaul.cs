using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Messaging.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTopicsOverhaul : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_messages_users_user_id",
                table: "messages");

            migrationBuilder.AddColumn<bool>(
                name: "is_protected",
                table: "rooms",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "owner_id",
                table: "rooms",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "sidebar_group_id",
                table: "room_memberships",
                type: "uuid",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "user_id",
                table: "messages",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<bool>(
                name: "is_system",
                table: "messages",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "sidebar_groups",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    display_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    is_collapsed = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sidebar_groups", x => x.id);
                    table.ForeignKey(
                        name: "FK_sidebar_groups_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_room_memberships_sidebar_group_id",
                table: "room_memberships",
                column: "sidebar_group_id");

            migrationBuilder.CreateIndex(
                name: "idx_sidebar_groups_user_id",
                table: "sidebar_groups",
                column: "user_id");

            migrationBuilder.AddForeignKey(
                name: "FK_messages_users_user_id",
                table: "messages",
                column: "user_id",
                principalTable: "users",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "FK_room_memberships_sidebar_groups_sidebar_group_id",
                table: "room_memberships",
                column: "sidebar_group_id",
                principalTable: "sidebar_groups",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_messages_users_user_id",
                table: "messages");

            migrationBuilder.DropForeignKey(
                name: "FK_room_memberships_sidebar_groups_sidebar_group_id",
                table: "room_memberships");

            migrationBuilder.DropTable(
                name: "sidebar_groups");

            migrationBuilder.DropIndex(
                name: "IX_room_memberships_sidebar_group_id",
                table: "room_memberships");

            migrationBuilder.DropColumn(
                name: "is_protected",
                table: "rooms");

            migrationBuilder.DropColumn(
                name: "owner_id",
                table: "rooms");

            migrationBuilder.DropColumn(
                name: "sidebar_group_id",
                table: "room_memberships");

            migrationBuilder.DropColumn(
                name: "is_system",
                table: "messages");

            migrationBuilder.AlterColumn<Guid>(
                name: "user_id",
                table: "messages",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_messages_users_user_id",
                table: "messages",
                column: "user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
