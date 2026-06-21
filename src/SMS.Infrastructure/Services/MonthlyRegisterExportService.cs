using System.Globalization;
using System.Text;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SMS.Application.DTOs;
using SMS.Application.Interfaces;
using SMS.Domain.Enums;

namespace SMS.Infrastructure.Services;

public class MonthlyRegisterExportService : IMonthlyRegisterExportService
{
    static MonthlyRegisterExportService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] BuildCsv(MonthlyRegisterDto register, RegisterExportMetadata metadata)
    {
        var sb = new StringBuilder();
        var monthName = CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(register.Month);

        sb.AppendLine($"School,{EscapeCsv(metadata.SchoolName)}");
        if (!string.IsNullOrWhiteSpace(metadata.SchoolAddress))
        {
            sb.AppendLine($"Address,{EscapeCsv(metadata.SchoolAddress)}");
        }

        if (!string.IsNullOrWhiteSpace(metadata.SchoolPhone))
        {
            sb.AppendLine($"Phone,{EscapeCsv(metadata.SchoolPhone)}");
        }

        sb.AppendLine($"Section,{EscapeCsv(register.SectionName)}");
        sb.AppendLine($"Month,{monthName} {register.Year}");
        sb.AppendLine($"Total Students,{register.Students.Count}");
        var workingDayCount = register.Students.Count > 0
            ? register.Students[0].Days.Count(x => !x.IsNonWorkingDay)
            : register.Dates.Count;
        sb.AppendLine($"Working Days,{workingDayCount}");
        sb.AppendLine();
        sb.AppendLine("Legend,P=Present,A=Absent,L=Late,Lv=Leave,H=Holiday,-=Not marked");
        sb.AppendLine();

        sb.Append("Roll No,Student Name");
        foreach (var date in register.Dates)
        {
            sb.Append(',').Append(date.ToString("dd-MMM", CultureInfo.InvariantCulture));
        }

        sb.AppendLine();

        foreach (var row in register.Students)
        {
            sb.Append(EscapeCsv(row.RollNumber)).Append(',').Append(EscapeCsv(row.StudentName));
            foreach (var cell in row.Days)
            {
                sb.Append(',').Append(GetCellCode(cell.Status));
            }

            sb.AppendLine();
        }

        sb.AppendLine();
        sb.AppendLine($"Paper,US Letter (Landscape)");
        sb.AppendLine($"Generated (UTC),{DateTime.UtcNow:yyyy-MM-dd HH:mm}");

        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
    }

    public byte[] BuildPdf(MonthlyRegisterDto register, RegisterExportMetadata metadata)
    {
        var monthName = CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(register.Month);
        var studentCount = register.Students.Count;
        var workingDays = register.Students.Count > 0
            ? register.Students[0].Days.Count(x => !x.IsNonWorkingDay)
            : register.Dates.Count;

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter.Landscape());
                page.MarginHorizontal(24);
                page.MarginVertical(18);
                page.DefaultTextStyle(x => x.FontSize(7).FontFamily("Arial"));

                page.Header().Column(column =>
                {
                    column.Item().Text(metadata.SchoolName).Bold().FontSize(13);
                    column.Item().Text($"Monthly Attendance Register — {register.SectionName} — {monthName} {register.Year}")
                        .FontSize(10)
                        .FontColor(Colors.Grey.Darken2);
                    if (!string.IsNullOrWhiteSpace(metadata.SchoolAddress))
                    {
                        column.Item().Text(metadata.SchoolAddress).FontSize(7).FontColor(Colors.Grey.Darken1);
                    }

                    column.Item().PaddingTop(4).Text($"Students: {studentCount}   |   Days in month: {register.Dates.Count}   |   Working days: {workingDays}")
                        .FontSize(7);
                });

                page.Content().PaddingVertical(6).Column(content =>
                {
                    content.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(34);
                            columns.RelativeColumn(2.2f);
                            foreach (var _ in register.Dates)
                            {
                                columns.ConstantColumn(14);
                            }
                        });

                        table.Header(header =>
                        {
                            header.Cell().Border(0.5f).Background(Colors.Grey.Lighten3).Padding(2).Text("Roll").Bold();
                            header.Cell().Border(0.5f).Background(Colors.Grey.Lighten3).Padding(2).Text("Student").Bold();
                            foreach (var date in register.Dates)
                            {
                                header.Cell().Border(0.5f).Background(Colors.Grey.Lighten3).Padding(1).AlignCenter().Text(date.Day.ToString()).Bold();
                            }
                        });

                        foreach (var row in register.Students)
                        {
                            table.Cell().Border(0.5f).Padding(2).Text(row.RollNumber);
                            table.Cell().Border(0.5f).Padding(2).Text(row.StudentName);
                            foreach (var cell in row.Days)
                            {
                                var background = cell.IsNonWorkingDay ? Colors.Grey.Lighten4 : Colors.White;
                                table.Cell().Border(0.5f).Background(background).Padding(1).AlignCenter()
                                    .Text(GetCellCode(cell.Status));
                            }
                        }
                    });

                    content.Item().PaddingTop(4).Text("P=Present  A=Absent  L=Late  Lv=Leave  H=Holiday  -=Not marked")
                        .FontSize(6.5f)
                        .FontColor(Colors.Grey.Darken1);
                });

                page.Footer().Column(footer =>
                {
                    footer.Item().LineHorizontal(0.75f).LineColor(Colors.Grey.Darken1);
                    footer.Item().PaddingTop(4).Row(row =>
                    {
                        row.RelativeItem().Text(metadata.SchoolName).SemiBold().FontSize(7);
                        row.RelativeItem().AlignCenter().Text($"US Letter (Landscape)  |  Students: {studentCount}  |  {monthName} {register.Year}")
                            .FontSize(7);
                        row.RelativeItem().AlignRight().Text(text =>
                        {
                            text.Span("Page ").FontSize(7);
                            text.CurrentPageNumber().FontSize(7);
                            text.Span(" of ").FontSize(7);
                            text.TotalPages().FontSize(7);
                        });
                    });
                });
            });
        });

        return document.GeneratePdf();
    }

    private static string GetCellCode(AttendanceStatus? status) => status switch
    {
        AttendanceStatus.Present => "P",
        AttendanceStatus.Absent => "A",
        AttendanceStatus.Late => "L",
        AttendanceStatus.Leave => "Lv",
        AttendanceStatus.Holiday => "H",
        _ => "-"
    };

    private static string EscapeCsv(string value)
    {
        if (value.Contains('"') || value.Contains(',') || value.Contains('\n') || value.Contains('\r'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }
}
