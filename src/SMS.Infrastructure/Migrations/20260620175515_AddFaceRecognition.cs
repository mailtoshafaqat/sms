using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFaceRecognition : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Modality",
                schema: "attendance",
                table: "StudentBiometricMaps",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "Modality",
                schema: "attendance",
                table: "BiometricDevices",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "ScanModality",
                schema: "attendance",
                table: "AttendanceLogs",
                type: "int",
                nullable: false,
                defaultValue: 1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Modality",
                schema: "attendance",
                table: "StudentBiometricMaps");

            migrationBuilder.DropColumn(
                name: "Modality",
                schema: "attendance",
                table: "BiometricDevices");

            migrationBuilder.DropColumn(
                name: "ScanModality",
                schema: "attendance",
                table: "AttendanceLogs");
        }
    }
}

