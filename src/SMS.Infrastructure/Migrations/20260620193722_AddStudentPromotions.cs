using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStudentPromotions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StudentPromotions",
                schema: "shared",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    AcademicYearId = table.Column<int>(type: "int", nullable: false),
                    FromSectionId = table.Column<int>(type: "int", nullable: false),
                    ToSectionId = table.Column<int>(type: "int", nullable: false),
                    Source = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PromotedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    PromotedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentPromotions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentPromotions_AcademicYears_AcademicYearId",
                        column: x => x.AcademicYearId,
                        principalSchema: "shared",
                        principalTable: "AcademicYears",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentPromotions_Sections_FromSectionId",
                        column: x => x.FromSectionId,
                        principalSchema: "shared",
                        principalTable: "Sections",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_StudentPromotions_Sections_ToSectionId",
                        column: x => x.ToSectionId,
                        principalSchema: "shared",
                        principalTable: "Sections",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_StudentPromotions_Students_StudentId",
                        column: x => x.StudentId,
                        principalSchema: "shared",
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StudentPromotions_AcademicYearId",
                schema: "shared",
                table: "StudentPromotions",
                column: "AcademicYearId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentPromotions_FromSectionId",
                schema: "shared",
                table: "StudentPromotions",
                column: "FromSectionId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentPromotions_StudentId_PromotedAt",
                schema: "shared",
                table: "StudentPromotions",
                columns: new[] { "StudentId", "PromotedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_StudentPromotions_ToSectionId",
                schema: "shared",
                table: "StudentPromotions",
                column: "ToSectionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StudentPromotions",
                schema: "shared");
        }
    }
}
