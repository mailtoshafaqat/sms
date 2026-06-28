using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SMS.Infrastructure.Data;

#nullable disable

namespace SMS.Infrastructure.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260621150000_AddStaffDeviceIds")]
    public partial class AddStaffDeviceIds : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF COL_LENGTH('shared.Teachers', 'FingerprintUserId') IS NULL
                    ALTER TABLE [shared].[Teachers] ADD [FingerprintUserId] nvarchar(50) NULL;
                IF COL_LENGTH('shared.Teachers', 'FaceUserId') IS NULL
                    ALTER TABLE [shared].[Teachers] ADD [FaceUserId] nvarchar(50) NULL;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF COL_LENGTH('shared.Teachers', 'FingerprintUserId') IS NOT NULL
                    ALTER TABLE [shared].[Teachers] DROP COLUMN [FingerprintUserId];
                IF COL_LENGTH('shared.Teachers', 'FaceUserId') IS NOT NULL
                    ALTER TABLE [shared].[Teachers] DROP COLUMN [FaceUserId];
                """);
        }
    }
}
