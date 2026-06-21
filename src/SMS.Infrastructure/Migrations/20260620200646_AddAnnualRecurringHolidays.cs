using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAnnualRecurringHolidays : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SchoolHolidays_SchoolId_HolidayDate",
                schema: "attendance",
                table: "SchoolHolidays");

            migrationBuilder.AddColumn<int>(
                name: "RecurringDay",
                schema: "attendance",
                table: "SchoolHolidays",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RecurringMonth",
                schema: "attendance",
                table: "SchoolHolidays",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "RepeatsAnnually",
                schema: "attendance",
                table: "SchoolHolidays",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_SchoolHolidays_SchoolId_HolidayDate",
                schema: "attendance",
                table: "SchoolHolidays",
                columns: new[] { "SchoolId", "HolidayDate" },
                unique: true,
                filter: "[RepeatsAnnually] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_SchoolHolidays_SchoolId_RecurringMonth_RecurringDay",
                schema: "attendance",
                table: "SchoolHolidays",
                columns: new[] { "SchoolId", "RecurringMonth", "RecurringDay" },
                unique: true,
                filter: "[RepeatsAnnually] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SchoolHolidays_SchoolId_HolidayDate",
                schema: "attendance",
                table: "SchoolHolidays");

            migrationBuilder.DropIndex(
                name: "IX_SchoolHolidays_SchoolId_RecurringMonth_RecurringDay",
                schema: "attendance",
                table: "SchoolHolidays");

            migrationBuilder.DropColumn(
                name: "RecurringDay",
                schema: "attendance",
                table: "SchoolHolidays");

            migrationBuilder.DropColumn(
                name: "RecurringMonth",
                schema: "attendance",
                table: "SchoolHolidays");

            migrationBuilder.DropColumn(
                name: "RepeatsAnnually",
                schema: "attendance",
                table: "SchoolHolidays");

            migrationBuilder.CreateIndex(
                name: "IX_SchoolHolidays_SchoolId_HolidayDate",
                schema: "attendance",
                table: "SchoolHolidays",
                columns: new[] { "SchoolId", "HolidayDate" },
                unique: true);
        }
    }
}
