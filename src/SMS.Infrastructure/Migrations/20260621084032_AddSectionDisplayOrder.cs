using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSectionDisplayOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DisplayOrder",
                schema: "shared",
                table: "Sections",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql("""
                WITH OrderedSections AS (
                    SELECT
                        Id,
                        ROW_NUMBER() OVER (PARTITION BY ClassRoomId ORDER BY Name) AS RowNum
                    FROM shared.Sections
                )
                UPDATE s
                SET DisplayOrder = o.RowNum
                FROM shared.Sections s
                INNER JOIN OrderedSections o ON s.Id = o.Id;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DisplayOrder",
                schema: "shared",
                table: "Sections");
        }
    }
}
