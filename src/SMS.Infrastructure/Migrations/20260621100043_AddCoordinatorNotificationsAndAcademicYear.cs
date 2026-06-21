using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCoordinatorNotificationsAndAcademicYear : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "NotifyAbsent",
                schema: "shared",
                table: "Schools",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "NotifyLate",
                schema: "shared",
                table: "Schools",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "AttendanceNotificationLogs",
                schema: "attendance",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    AttendanceDate = table.Column<DateOnly>(type: "date", nullable: false),
                    NotificationType = table.Column<int>(type: "int", nullable: false),
                    RecipientPhone = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    IsSent = table.Column<bool>(type: "bit", nullable: false),
                    SentAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceNotificationLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AttendanceNotificationLogs_Students_StudentId",
                        column: x => x.StudentId,
                        principalSchema: "shared",
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceNotificationLogs_SchoolId_AttendanceDate_StudentId_NotificationType",
                schema: "attendance",
                table: "AttendanceNotificationLogs",
                columns: new[] { "SchoolId", "AttendanceDate", "StudentId", "NotificationType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceNotificationLogs_StudentId",
                schema: "attendance",
                table: "AttendanceNotificationLogs",
                column: "StudentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AttendanceNotificationLogs",
                schema: "attendance");

            migrationBuilder.DropColumn(
                name: "NotifyAbsent",
                schema: "shared",
                table: "Schools");

            migrationBuilder.DropColumn(
                name: "NotifyLate",
                schema: "shared",
                table: "Schools");
        }
    }
}
