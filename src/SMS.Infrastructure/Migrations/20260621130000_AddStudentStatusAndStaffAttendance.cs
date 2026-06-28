using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SMS.Infrastructure.Data;

#nullable disable

namespace SMS.Infrastructure.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260621130000_AddStudentStatusAndStaffAttendance")]
    public partial class AddStudentStatusAndStaffAttendance : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF COL_LENGTH('shared.Students', 'Status') IS NULL
                    ALTER TABLE [shared].[Students] ADD [Status] int NOT NULL CONSTRAINT DF_Students_Status DEFAULT 1;
                IF COL_LENGTH('shared.Students', 'StatusNote') IS NULL
                    ALTER TABLE [shared].[Students] ADD [StatusNote] nvarchar(500) NULL;
                """);

            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[attendance].[StaffDailyAttendances]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [attendance].[StaffDailyAttendances] (
                        [Id] int NOT NULL IDENTITY,
                        [SchoolId] int NOT NULL,
                        [TeacherId] int NOT NULL,
                        [AttendanceDate] date NOT NULL,
                        [Status] int NOT NULL,
                        [CheckInTime] datetime2 NULL,
                        [CheckOutTime] datetime2 NULL,
                        [Remarks] nvarchar(250) NULL,
                        [IsManualEntry] bit NOT NULL,
                        [UpdatedByUserId] nvarchar(450) NULL,
                        [CreatedAt] datetime2 NOT NULL,
                        [UpdatedAt] datetime2 NULL,
                        CONSTRAINT [PK_StaffDailyAttendances] PRIMARY KEY ([Id]),
                        CONSTRAINT [FK_StaffDailyAttendances_Teachers_TeacherId] FOREIGN KEY ([TeacherId]) REFERENCES [shared].[Teachers] ([Id]) ON DELETE NO ACTION
                    );
                    CREATE INDEX [IX_StaffDailyAttendances_SchoolId_AttendanceDate] ON [attendance].[StaffDailyAttendances] ([SchoolId], [AttendanceDate]);
                    CREATE UNIQUE INDEX [IX_StaffDailyAttendances_TeacherId_AttendanceDate] ON [attendance].[StaffDailyAttendances] ([TeacherId], [AttendanceDate]);
                END
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StaffDailyAttendances",
                schema: "attendance");

            migrationBuilder.DropColumn(
                name: "Status",
                schema: "shared",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "StatusNote",
                schema: "shared",
                table: "Students");
        }
    }
}
