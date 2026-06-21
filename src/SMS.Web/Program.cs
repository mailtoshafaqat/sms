using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;
using SMS.Application.Interfaces;
using SMS.Infrastructure;
using SMS.Infrastructure.Data;
using SMS.Infrastructure.Identity;
using SMS.Web;
using SMS.Web.Components;
using SMS.Web.Configuration;
using SMS.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<AppBrandingOptions>(
    builder.Configuration.GetSection(AppBrandingOptions.SectionName));

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddHttpContextAccessor();
builder.Services.AddAuthorization();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();
builder.Services.AddScoped<SchoolBrandingRefresh>();
builder.Services.AddSingleton<ToastService>();
builder.Services.AddSingleton<ConfirmDialogService>();

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 2 * 1024 * 1024;
});

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

if (builder.Environment.IsDevelopment())
{
    builder.Services.Configure<Microsoft.AspNetCore.Components.Server.CircuitOptions>(
        options => options.DetailedErrors = true);
}

var app = builder.Build();

await DatabaseSeeder.SeedAsync(app.Services);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapGet("/settings/backup/download/{fileName}", async (
    string fileName,
    IDatabaseBackupService backupService,
    CancellationToken cancellationToken) =>
{
    var stream = await backupService.OpenBackupReadStreamAsync(fileName, cancellationToken);
    if (stream is null)
    {
        return Results.NotFound();
    }

    return Results.File(stream, "application/octet-stream", fileName);
}).RequireAuthorization(new AuthorizeAttribute { Roles = "Admin" });

app.MapGet("/attendance/register/export/csv", async (
    int sectionId,
    int year,
    int month,
    IAttendanceService attendanceService,
    ISchoolService schoolService,
    IMonthlyRegisterExportService exportService,
    HttpContext httpContext,
    CancellationToken cancellationToken) =>
{
    var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
    var register = await attendanceService.GetMonthlyRegisterAsync(sectionId, year, month, userId, cancellationToken);
    var school = await schoolService.GetSchoolSettingsAsync(cancellationToken);
    var metadata = new RegisterExportMetadata(
        school?.Name ?? "School",
        school?.Address,
        school?.Phone);
    var bytes = exportService.BuildCsv(register, metadata);
    var fileName = BuildRegisterFileName(register.SectionName, year, month, "csv");
    return Results.File(bytes, "text/csv", fileName);
}).RequireAuthorization();

app.MapGet("/attendance/register/export/pdf", async (
    int sectionId,
    int year,
    int month,
    IAttendanceService attendanceService,
    ISchoolService schoolService,
    IMonthlyRegisterExportService exportService,
    HttpContext httpContext,
    CancellationToken cancellationToken) =>
{
    var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
    var register = await attendanceService.GetMonthlyRegisterAsync(sectionId, year, month, userId, cancellationToken);
    var school = await schoolService.GetSchoolSettingsAsync(cancellationToken);
    var metadata = new RegisterExportMetadata(
        school?.Name ?? "School",
        school?.Address,
        school?.Phone);
    var bytes = exportService.BuildPdf(register, metadata);
    var fileName = BuildRegisterFileName(register.SectionName, year, month, "pdf");
    return Results.File(bytes, "application/pdf", fileName);
}).RequireAuthorization();

static string BuildRegisterFileName(string sectionName, int year, int month, string extension)
{
    var safeSection = string.Concat(sectionName.Where(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' or ' ')).Trim();
    safeSection = safeSection.Replace(' ', '_');
    if (string.IsNullOrWhiteSpace(safeSection))
    {
        safeSection = "Section";
    }

    return $"Attendance_{safeSection}_{year}_{month:D2}.{extension}";
}

app.Run();

