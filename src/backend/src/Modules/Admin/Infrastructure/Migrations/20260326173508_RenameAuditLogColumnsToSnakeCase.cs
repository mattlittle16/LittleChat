using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Admin.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenameAuditLogColumnsToSnakeCase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Action",
                table: "admin_audit_log",
                newName: "action");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "admin_audit_log",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "TargetName",
                table: "admin_audit_log",
                newName: "target_name");

            migrationBuilder.RenameColumn(
                name: "TargetId",
                table: "admin_audit_log",
                newName: "target_id");

            migrationBuilder.RenameColumn(
                name: "OccurredAt",
                table: "admin_audit_log",
                newName: "occurred_at");

            migrationBuilder.RenameColumn(
                name: "AdminName",
                table: "admin_audit_log",
                newName: "admin_name");

            migrationBuilder.RenameColumn(
                name: "AdminId",
                table: "admin_audit_log",
                newName: "admin_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "action",
                table: "admin_audit_log",
                newName: "Action");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "admin_audit_log",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "target_name",
                table: "admin_audit_log",
                newName: "TargetName");

            migrationBuilder.RenameColumn(
                name: "target_id",
                table: "admin_audit_log",
                newName: "TargetId");

            migrationBuilder.RenameColumn(
                name: "occurred_at",
                table: "admin_audit_log",
                newName: "OccurredAt");

            migrationBuilder.RenameColumn(
                name: "admin_name",
                table: "admin_audit_log",
                newName: "AdminName");

            migrationBuilder.RenameColumn(
                name: "admin_id",
                table: "admin_audit_log",
                newName: "AdminId");
        }
    }
}
