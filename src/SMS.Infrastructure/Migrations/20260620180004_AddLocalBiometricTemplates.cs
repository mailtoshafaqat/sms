using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLocalBiometricTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StudentLocalTemplates",
                schema: "attendance",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    BiometricType = table.Column<int>(type: "int", nullable: false),
                    TemplateData = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ExternalId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentLocalTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentLocalTemplates_Students_StudentId",
                        column: x => x.StudentId,
                        principalSchema: "shared",
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StudentLocalTemplates_ExternalId",
                schema: "attendance",
                table: "StudentLocalTemplates",
                column: "ExternalId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentLocalTemplates_StudentId_BiometricType",
                schema: "attendance",
                table: "StudentLocalTemplates",
                columns: new[] { "StudentId", "BiometricType" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StudentLocalTemplates",
                schema: "attendance");
        }
    }
}

