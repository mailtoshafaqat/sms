using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;
using SMS.Application.DTOs;
using SMS.Application.Interfaces;
using SMS.Domain.Common;
using SMS.Domain.Enums;
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
builder.Services.AddScoped<IUserPrincipalAccessor, UserPrincipalAccessor>();
builder.Services.AddSingleton<ToastService>();
builder.Services.AddSingleton<ConfirmDialogService>();

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 2 * 1024 * 1024;
});

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.Configure<Microsoft.AspNetCore.Components.Server.CircuitOptions>(options =>
{
    options.DisconnectedCircuitMaxRetained = 100;
    options.DisconnectedCircuitRetentionPeriod = TimeSpan.FromMinutes(3);
    options.JSInteropDefaultCallTimeout = TimeSpan.FromMinutes(2);
    if (builder.Environment.IsDevelopment())
    {
        options.DetailedErrors = true;
    }
});

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

app.MapGet("/attendance/staff-register/export/csv", async (
    int year,
    int month,
    IStaffAttendanceService staffAttendanceService,
    ISchoolService schoolService,
    IStaffMonthlyRegisterExportService exportService,
    CancellationToken cancellationToken) =>
{
    var register = await staffAttendanceService.GetMonthlyRegisterAsync(year, month, cancellationToken);
    var school = await schoolService.GetSchoolSettingsAsync(cancellationToken);
    var metadata = new RegisterExportMetadata(
        school?.Name ?? "School",
        school?.Address,
        school?.Phone);
    var bytes = exportService.BuildCsv(register, metadata);
    var fileName = BuildStaffRegisterFileName(year, month, "csv");
    return Results.File(bytes, "text/csv", fileName);
}).RequireAuthorization(new AuthorizeAttribute { Roles = $"{RoleNames.Admin},{RoleNames.Coordinator}" });

app.MapGet("/attendance/staff-register/export/pdf", async (
    int year,
    int month,
    IStaffAttendanceService staffAttendanceService,
    ISchoolService schoolService,
    IStaffMonthlyRegisterExportService exportService,
    CancellationToken cancellationToken) =>
{
    var register = await staffAttendanceService.GetMonthlyRegisterAsync(year, month, cancellationToken);
    var school = await schoolService.GetSchoolSettingsAsync(cancellationToken);
    var metadata = new RegisterExportMetadata(
        school?.Name ?? "School",
        school?.Address,
        school?.Phone);
    var bytes = exportService.BuildPdf(register, metadata);
    var fileName = BuildStaffRegisterFileName(year, month, "pdf");
    return Results.File(bytes, "application/pdf", fileName);
}).RequireAuthorization(new AuthorizeAttribute { Roles = $"{RoleNames.Admin},{RoleNames.Coordinator}" });

app.MapPost("/attendance/gate/scan", async (
    GateFaceScanRequest request,
    ILocalBiometricService localBiometricService,
    CancellationToken cancellationToken) =>
{
    if (request.Descriptor is null || request.Descriptor.Length < 32)
    {
        return Results.BadRequest(new GateFaceScanResponse(false, "Invalid face scan data."));
    }

    var match = await localBiometricService.MatchFaceAsync(request.Descriptor, request.MatchMode, cancellationToken);
    if (match is null)
    {
        return Results.Ok(new GateFaceScanResponse(false,
            "Face not recognized. Enroll the student at Attendance → Biometric Test, then scan again."));
    }

    var result = await localBiometricService.ScanByExternalIdAsync(
        match.ExternalId,
        BiometricType.Face,
        cancellationToken: cancellationToken);

    var message = result.Success
        ? $"{match.StudentName} — {result.Message}"
        : result.Message;

    return Results.Ok(new GateFaceScanResponse(result.Success, message, result.Success ? match.StudentName : null));
}).RequireAuthorization().DisableAntiforgery();

app.MapGet("/attendance/gate/enrollments", async (
    ILocalBiometricService localBiometricService,
    CancellationToken cancellationToken) =>
    Results.Ok(await localBiometricService.GetFaceEnrollmentsAsync(cancellationToken)))
    .RequireAuthorization();

app.MapPost("/attendance/gate/enroll", async (
    GateFaceEnrollRequest request,
    ILocalBiometricService localBiometricService,
    CancellationToken cancellationToken) =>
{
    if (request.StudentId <= 0)
    {
        return Results.BadRequest(new GateFaceEnrollResponse(false, "Select a student.", 0, false));
    }

    if (request.Descriptor is null || request.Descriptor.Length < 32)
    {
        return Results.BadRequest(new GateFaceEnrollResponse(false, "No face detected. Face the camera in good light.", 0, false));
    }

    await localBiometricService.EnrollFaceAsync(request.StudentId, request.Descriptor, cancellationToken);
    var sampleCount = await localBiometricService.GetFaceSampleCountAsync(request.StudentId, cancellationToken);
    var verify = await localBiometricService.MatchFaceAsync(request.Descriptor, FaceMatchMode.Gate, cancellationToken);
    var gateReady = verify is not null && sampleCount >= 2;

    var message = gateReady
        ? $"Saved sample {sampleCount}. Gate ready for this student."
        : $"Saved sample {sampleCount}. Click Enroll again from a slightly different angle.";

    return Results.Ok(new GateFaceEnrollResponse(true, message, sampleCount, gateReady));
}).RequireAuthorization(new AuthorizeAttribute { Roles = $"{RoleNames.Admin},{RoleNames.Coordinator}" }).DisableAntiforgery();

app.MapPost("/attendance/gate/record", async (
    GateFaceRecordRequest request,
    ILocalBiometricService localBiometricService,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.ExternalId))
    {
        return Results.BadRequest(new GateFaceScanResponse(false, "Missing student enrollment id."));
    }

    var result = await localBiometricService.ScanByExternalIdAsync(
        request.ExternalId.Trim(),
        BiometricType.Face,
        cancellationToken: cancellationToken);

    var message = result.Success && result.Match is not null
        ? $"{result.Match.StudentName} — {result.Message}"
        : result.Message;

    return Results.Ok(new GateFaceScanResponse(result.Success, message, result.Match?.StudentName));
}).RequireAuthorization().DisableAntiforgery();

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

static string BuildStaffRegisterFileName(int year, int month, string extension) =>
    $"Staff_Attendance_{year}_{month:D2}.{extension}";

app.Run();

