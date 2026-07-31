using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Care.WebApi.Migrators.MySQL.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationPreferencesAndReminder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ReminderSentAt",
                table: "Shifts",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "NotifyDocumentUploadedEmail",
                table: "AppSettings",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "NotifyDocumentUploadedSms",
                table: "AppSettings",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "NotifyReplacementClaimedEmail",
                table: "AppSettings",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "NotifyReplacementClaimedSms",
                table: "AppSettings",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "NotifyReplacementRequestedEmail",
                table: "AppSettings",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "NotifyReplacementRequestedSms",
                table: "AppSettings",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "NotifyShiftAssignedEmail",
                table: "AppSettings",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "NotifyShiftAssignedSms",
                table: "AppSettings",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "NotifyShiftBoundaryChangedEmail",
                table: "AppSettings",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "NotifyShiftBoundaryChangedSms",
                table: "AppSettings",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "NotifyShiftReminderEmail",
                table: "AppSettings",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "NotifyShiftReminderSms",
                table: "AppSettings",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "NotifyShiftRemovedEmail",
                table: "AppSettings",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "NotifyShiftRemovedSms",
                table: "AppSettings",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReminderSentAt",
                table: "Shifts");

            migrationBuilder.DropColumn(
                name: "NotifyDocumentUploadedEmail",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "NotifyDocumentUploadedSms",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "NotifyReplacementClaimedEmail",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "NotifyReplacementClaimedSms",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "NotifyReplacementRequestedEmail",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "NotifyReplacementRequestedSms",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "NotifyShiftAssignedEmail",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "NotifyShiftAssignedSms",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "NotifyShiftBoundaryChangedEmail",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "NotifyShiftBoundaryChangedSms",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "NotifyShiftReminderEmail",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "NotifyShiftReminderSms",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "NotifyShiftRemovedEmail",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "NotifyShiftRemovedSms",
                table: "AppSettings");
        }
    }
}
