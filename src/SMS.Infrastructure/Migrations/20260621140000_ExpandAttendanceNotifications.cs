using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SMS.Infrastructure.Data;

#nullable disable

namespace SMS.Infrastructure.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260621140000_ExpandAttendanceNotifications")]
    public partial class ExpandAttendanceNotifications : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF COL_LENGTH('shared.Schools', 'NotifyCheckIn') IS NULL
                    ALTER TABLE [shared].[Schools] ADD [NotifyCheckIn] bit NOT NULL CONSTRAINT DF_Schools_NotifyCheckIn DEFAULT 1;
                IF COL_LENGTH('shared.Schools', 'NotifyCheckOut') IS NULL
                    ALTER TABLE [shared].[Schools] ADD [NotifyCheckOut] bit NOT NULL CONSTRAINT DF_Schools_NotifyCheckOut DEFAULT 1;
                IF COL_LENGTH('shared.Schools', 'NotifyLeave') IS NULL
                    ALTER TABLE [shared].[Schools] ADD [NotifyLeave] bit NOT NULL CONSTRAINT DF_Schools_NotifyLeave DEFAULT 1;
                IF COL_LENGTH('shared.Schools', 'NotifyPresent') IS NULL
                    ALTER TABLE [shared].[Schools] ADD [NotifyPresent] bit NOT NULL CONSTRAINT DF_Schools_NotifyPresent DEFAULT 1;
                IF COL_LENGTH('shared.Schools', 'CheckInNotificationTemplate') IS NULL
                    ALTER TABLE [shared].[Schools] ADD [CheckInNotificationTemplate] nvarchar(500) NULL;
                IF COL_LENGTH('shared.Schools', 'CheckOutNotificationTemplate') IS NULL
                    ALTER TABLE [shared].[Schools] ADD [CheckOutNotificationTemplate] nvarchar(500) NULL;
                IF COL_LENGTH('shared.Schools', 'LeaveNotificationTemplate') IS NULL
                    ALTER TABLE [shared].[Schools] ADD [LeaveNotificationTemplate] nvarchar(500) NULL;
                IF COL_LENGTH('shared.Schools', 'PresentNotificationTemplate') IS NULL
                    ALTER TABLE [shared].[Schools] ADD [PresentNotificationTemplate] nvarchar(500) NULL;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "NotifyCheckIn", schema: "shared", table: "Schools");
            migrationBuilder.DropColumn(name: "NotifyCheckOut", schema: "shared", table: "Schools");
            migrationBuilder.DropColumn(name: "NotifyLeave", schema: "shared", table: "Schools");
            migrationBuilder.DropColumn(name: "NotifyPresent", schema: "shared", table: "Schools");
            migrationBuilder.DropColumn(name: "CheckInNotificationTemplate", schema: "shared", table: "Schools");
            migrationBuilder.DropColumn(name: "CheckOutNotificationTemplate", schema: "shared", table: "Schools");
            migrationBuilder.DropColumn(name: "LeaveNotificationTemplate", schema: "shared", table: "Schools");
            migrationBuilder.DropColumn(name: "PresentNotificationTemplate", schema: "shared", table: "Schools");
        }
    }
}
