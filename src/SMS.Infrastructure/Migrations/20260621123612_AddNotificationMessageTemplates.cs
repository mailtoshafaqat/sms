using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationMessageTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AbsentNotificationTemplate",
                schema: "shared",
                table: "Schools",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LateNotificationTemplate",
                schema: "shared",
                table: "Schools",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AbsentNotificationTemplate",
                schema: "shared",
                table: "Schools");

            migrationBuilder.DropColumn(
                name: "LateNotificationTemplate",
                schema: "shared",
                table: "Schools");
        }
    }
}
