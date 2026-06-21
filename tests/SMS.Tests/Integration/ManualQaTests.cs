using System.Net;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using SMS.Application.DTOs;
using SMS.Application.Interfaces;
using SMS.Domain.Enums;
using SMS.Infrastructure.Identity;
using Xunit;
using Xunit.Abstractions;

namespace SMS.Tests.Integration;

public class ManualQaTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly ITestOutputHelper _output;

    public ManualQaTests(WebApplicationFactory<Program> factory, ITestOutputHelper output)
    {
        _factory = factory;
        _output = output;
    }

    [Fact]
    public async Task FullApplication_ManualQaChecklist()
    {
        var failures = new List<string>();
        var passes = new List<string>();

        void Pass(string message)
        {
            passes.Add(message);
            _output.WriteLine($"PASS: {message}");
        }

        void Fail(string message)
        {
            failures.Add(message);
            _output.WriteLine($"FAIL: {message}");
        }

        await using var scope = _factory.Services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();

        var admin = await userManager.FindByEmailAsync("admin@school.local")
            ?? throw new InvalidOperationException("Admin user missing.");
        var coordinator = await userManager.FindByEmailAsync("coordinator@school.local")
            ?? throw new InvalidOperationException("Coordinator user missing.");
        var teacher = await userManager.FindByEmailAsync("teacher@school.local")
            ?? throw new InvalidOperationException("Teacher user missing.");

        var authFactory = _factory.WithQaAuthentication();

        // --- Page access matrix ---
        var pages = new (string Path, bool Admin, bool Coordinator, bool Teacher)[]
        {
            ("/login", true, true, true),
            ("/", true, true, true),
            ("/help", true, true, true),
            ("/classes", true, true, true),
            ("/students", true, true, true),
            ("/students/edit", true, true, true),
            ("/students/promote", true, true, false),
            ("/attendance/daily", true, true, true),
            ("/attendance/manual", true, true, true),
            ("/attendance/register", true, true, true),
            ("/attendance/pattern-report", true, true, true),
            ("/attendance/late-report", true, true, true),
            ("/attendance/calendar", true, true, false),
            ("/attendance/notifications", true, true, false),
            ("/attendance/live", true, true, true),
            ("/attendance/gate", true, true, true),
            ("/attendance/local-test", true, false, false),
            ("/settings/school", true, false, false),
            ("/settings/users", true, false, false),
            ("/settings/staff", true, false, false),
            ("/settings/academic-years", true, true, false),
            ("/settings/biometric", true, true, true),
            ("/settings/backup", true, false, false),
            ("/settings/exception-logs", true, false, false)
        };

        foreach (var page in pages)
        {
            await AssertPageAccessAsync(authFactory, page.Path, "Admin", admin.Id, page.Admin, Fail, Pass);
            await AssertPageAccessAsync(authFactory, page.Path, "Coordinator", coordinator.Id, page.Coordinator, Fail, Pass);
            await AssertPageAccessAsync(authFactory, page.Path, "Teacher", teacher.Id, page.Teacher, Fail, Pass);
        }

        var anonClient = authFactory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        foreach (var page in pages.Where(p => p.Path != "/login"))
        {
            var response = await anonClient.GetAsync(page.Path);
            if (response.StatusCode is HttpStatusCode.Redirect or HttpStatusCode.Found or HttpStatusCode.Unauthorized)
            {
                Pass($"Anonymous {page.Path} blocked ({response.StatusCode})");
            }
            else
            {
                Fail($"Anonymous {page.Path} expected block, got {response.StatusCode}");
            }
        }

        // --- Service functionality ---
        await TestDashboardAsync(sp, admin.Id, teacher.Id, Fail, Pass);
        await TestAcademicYearsAsync(sp, Fail, Pass);
        await TestStaffAsync(sp, teacher, Fail, Pass);
        await TestNotificationsAsync(sp, Fail, Pass);
        await TestExceptionLogsAsync(sp, Fail, Pass);
        await TestUserAccessAsync(sp, admin.Id, coordinator.Id, teacher.Id, Fail, Pass);
        await TestMonthlyRegisterAndExportsAsync(authFactory, sp, admin.Id, Fail, Pass);
        await TestPromotionHistoryAsync(sp, Fail, Pass);
        await TestAttendanceCalendarAsync(sp, Fail, Pass);
        await TestAttendancePatternAsync(sp, Fail, Pass);
        await TestGatePwaAssetsAsync(authFactory, Fail, Pass);
        await TestBiometricDeviceSaveAsync(sp, Fail, Pass);
        await TestLocalFingerprintAsync(sp, Fail, Pass);
        await TestStudentSecondSaveAsync(sp, Fail, Pass);
        await TestStudentPhotoAsync(sp, Fail, Pass);
        await TestUsersManagementAsync(sp, Fail, Pass);

        _output.WriteLine($"--- SUMMARY: {passes.Count} passed, {failures.Count} failed ---");
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    private static async Task AssertPageAccessAsync(
        WebApplicationFactory<Program> factory,
        string path,
        string role,
        string userId,
        bool shouldAllow,
        Action<string> fail,
        Action<string> pass)
    {
        var client = factory.CreateRoleClient(role, userId);
        var response = await client.GetAsync(path);
        var html = await response.Content.ReadAsStringAsync();

        if (shouldAllow)
        {
            if (response.StatusCode != HttpStatusCode.OK)
            {
                fail($"{role} {path}: expected 200, got {response.StatusCode}");
                return;
            }

            if (html.Contains("You are not authorized.", StringComparison.OrdinalIgnoreCase))
            {
                fail($"{role} {path}: page returned not authorized");
                return;
            }

            if (html.Contains("InvalidOperationException", StringComparison.OrdinalIgnoreCase)
                || html.Contains("An unhandled exception", StringComparison.OrdinalIgnoreCase))
            {
                fail($"{role} {path}: page HTML contains exception text");
                return;
            }

            pass($"{role} {path}: accessible");
            return;
        }

        if (response.StatusCode is HttpStatusCode.Redirect or HttpStatusCode.Found)
        {
            pass($"{role} {path}: blocked (redirect)");
            return;
        }

        if (response.StatusCode == HttpStatusCode.Forbidden)
        {
            pass($"{role} {path}: blocked (forbidden)");
            return;
        }

        if (response.StatusCode == HttpStatusCode.OK
            && html.Contains("You are not authorized.", StringComparison.OrdinalIgnoreCase))
        {
            pass($"{role} {path}: blocked (not authorized)");
            return;
        }

        fail($"{role} {path}: expected block, got {response.StatusCode}");
    }

    private static async Task TestDashboardAsync(
        IServiceProvider sp,
        string adminId,
        string teacherId,
        Action<string> fail,
        Action<string> pass)
    {
        var dashboard = sp.GetRequiredService<IDashboardService>();
        var adminStats = await dashboard.GetDashboardAsync(adminId);
        var teacherStats = await dashboard.GetDashboardAsync(teacherId);

        if (adminStats.TodaySummary is null || teacherStats.TodaySummary is null)
        {
            fail("Dashboard: missing today summary");
            return;
        }

        pass($"Dashboard: admin enrolled={adminStats.TodaySummary.EnrolledStudents}, teacher enrolled={teacherStats.TodaySummary.EnrolledStudents}");
    }

    private static async Task TestAcademicYearsAsync(IServiceProvider sp, Action<string> fail, Action<string> pass)
    {
        var service = sp.GetRequiredService<IAcademicYearService>();
        var years = await service.GetYearsAsync();
        if (years.Count == 0)
        {
            fail("Academic years: none found");
            return;
        }

        var suffix = Guid.NewGuid().ToString("N")[..6];
        var newId = await service.SaveYearAsync(new AcademicYearFormDto
        {
            Name = $"QA-{suffix}",
            StartDate = new DateOnly(2099, 1, 1),
            EndDate = new DateOnly(2099, 12, 31)
        });

        var updated = await service.GetYearsAsync();
        if (!updated.Any(x => x.Id == newId))
        {
            fail("Academic years: save failed");
            return;
        }

        await service.SetCurrentYearAsync(years.First(x => x.IsCurrent).Id);
        await service.DeleteYearAsync(newId);
        pass($"Academic years: list={years.Count}, create/delete OK");
    }

    private static async Task TestStaffAsync(
        IServiceProvider sp,
        ApplicationUser teacherUser,
        Action<string> fail,
        Action<string> pass)
    {
        var service = sp.GetRequiredService<ITeacherAssignmentService>();
        var assignments = await service.GetSectionAssignmentsAsync();
        var teachers = await service.GetTeachersAsync();

        if (assignments.Count == 0)
        {
            fail("Staff: no sections for assignment");
            return;
        }

        var sectionId = assignments[0].SectionId;
        var teacher = teachers.FirstOrDefault(t => t.UserId == teacherUser.Id)
            ?? teachers.FirstOrDefault();

        if (teacher is null)
        {
            fail("Staff: no teacher profile found");
            return;
        }

        await service.AssignSectionTeacherAsync(sectionId, teacher.Id);
        var refreshed = await service.GetSectionAssignmentsAsync();
        if (refreshed.First(x => x.SectionId == sectionId).TeacherId != teacher.Id)
        {
            fail("Staff: assign section teacher failed");
            return;
        }

        await service.EnsureTeacherProfileAsync(teacherUser.Id, "Demo", "Teacher");
        pass($"Staff: {assignments.Count} sections, {teachers.Count} teachers, assign OK");
    }

    private static async Task TestNotificationsAsync(IServiceProvider sp, Action<string> fail, Action<string> pass)
    {
        var service = sp.GetRequiredService<IAttendanceNotificationService>();
        var today = DateOnly.FromDateTime(DateTime.Today);

        await service.QueueAbsentNotificationsAsync(today);
        var list = await service.GetNotificationsAsync(today);
        var url = service.BuildWhatsAppUrl("923001234567", "Test message");

        if (!url.StartsWith("https://wa.me/", StringComparison.OrdinalIgnoreCase))
        {
            fail("Notifications: invalid WhatsApp URL");
            return;
        }

        pass($"Notifications: queue OK, {list.Count} item(s), wa.me URL OK");
    }

    private static async Task TestExceptionLogsAsync(IServiceProvider sp, Action<string> fail, Action<string> pass)
    {
        var service = sp.GetRequiredService<IExceptionLogService>();
        var id = await service.LogAsync(new InvalidOperationException("QA test exception"), "ManualQaTests");
        var page = await service.GetLogsAsync(search: "QA test", page: 1, pageSize: 10);
        var detail = await service.GetLogAsync(id);

        if (detail is null || !page.Items.Any(x => x.Id == id))
        {
            fail("Exception logs: log/read failed");
            return;
        }

        pass($"Exception logs: wrote id={id}, list count={page.Items.Count}");
    }

    private static async Task TestUserAccessAsync(
        IServiceProvider sp,
        string adminId,
        string coordinatorId,
        string teacherId,
        Action<string> fail,
        Action<string> pass)
    {
        var access = sp.GetRequiredService<IUserAccessService>();
        var classService = sp.GetRequiredService<IClassService>();

        if (!await access.IsAdminAsync(adminId))
        {
            fail("User access: admin check failed");
            return;
        }

        if (!await access.IsCoordinatorAsync(coordinatorId))
        {
            fail("User access: coordinator check failed");
            return;
        }

        if (await access.HasFullAttendanceAccessAsync(teacherId))
        {
            fail("User access: teacher should not have full attendance access");
            return;
        }

        var allSections = await classService.GetSectionOptionsAsync();
        var teacherSections = await classService.GetSectionOptionsAsync(teacherId);
        var allowed = await access.GetAllowedSectionIdsAsync(teacherId);

        if (teacherSections.Count == 0 || allowed.Count == 0)
        {
            fail("User access: teacher has no assigned sections");
            return;
        }

        if (teacherSections.Count >= allSections.Count && allSections.Count > 1)
        {
            fail("User access: teacher sees all sections");
            return;
        }

        pass($"User access: teacher limited to {teacherSections.Count}/{allSections.Count} sections");
    }

    private static async Task TestMonthlyRegisterAndExportsAsync(
        WebApplicationFactory<Program> authFactory,
        IServiceProvider sp,
        string adminId,
        Action<string> fail,
        Action<string> pass)
    {
        var attendance = sp.GetRequiredService<IAttendanceService>();
        var export = sp.GetRequiredService<IMonthlyRegisterExportService>();
        var school = sp.GetRequiredService<ISchoolService>();
        var classService = sp.GetRequiredService<IClassService>();

        var sections = await classService.GetSectionOptionsAsync(adminId);
        if (sections.Count == 0)
        {
            fail("Monthly register: no sections");
            return;
        }

        var sectionId = sections[0].SectionId;
        var now = DateTime.Today;
        var register = await attendance.GetMonthlyRegisterAsync(sectionId, now.Year, now.Month, adminId);
        var settings = await school.GetSchoolSettingsAsync();
        var metadata = new RegisterExportMetadata(settings?.Name ?? "School", settings?.Address, settings?.Phone);

        var csv = export.BuildCsv(register, metadata);
        var pdf = export.BuildPdf(register, metadata);

        if (csv.Length < 10 || pdf.Length < 100)
        {
            fail("Monthly register: export bytes too small");
            return;
        }

        var client = authFactory.CreateRoleClient("Admin", adminId);
        var csvResponse = await client.GetAsync($"/attendance/register/export/csv?sectionId={sectionId}&year={now.Year}&month={now.Month}");
        var pdfResponse = await client.GetAsync($"/attendance/register/export/pdf?sectionId={sectionId}&year={now.Year}&month={now.Month}");

        if (csvResponse.StatusCode != HttpStatusCode.OK || pdfResponse.StatusCode != HttpStatusCode.OK)
        {
            fail($"Monthly register HTTP export failed: csv={csvResponse.StatusCode}, pdf={pdfResponse.StatusCode}");
            return;
        }

        var csvBody = await csvResponse.Content.ReadAsByteArrayAsync();
        var pdfBody = await pdfResponse.Content.ReadAsByteArrayAsync();
        if (csvBody.Length < 10 || pdfBody.Length < 100)
        {
            fail("Monthly register HTTP export body too small");
            return;
        }

        pass($"Monthly register: {register.Students.Count} students, CSV={csv.Length}b, PDF={pdf.Length}b, HTTP exports OK");
    }

    private static async Task TestPromotionHistoryAsync(IServiceProvider sp, Action<string> fail, Action<string> pass)
    {
        var service = sp.GetRequiredService<IStudentPromotionService>();
        var history = await service.GetRecentHistoryAsync(5);
        pass($"Promotion history: {history.Count} recent record(s)");
    }

    private static async Task TestAttendanceCalendarAsync(IServiceProvider sp, Action<string> fail, Action<string> pass)
    {
        var service = sp.GetRequiredService<IAttendanceService>();
        var now = DateTime.Today;
        var calendar = await service.GetAttendanceCalendarAsync(now.Year, now.Month);
        if (calendar.Days.Count == 0)
        {
            fail("Attendance calendar: no days returned");
            return;
        }

        pass($"Attendance calendar: {calendar.Days.Count} day(s) for {now:yyyy-MM}");
    }

    private static async Task TestAttendancePatternAsync(IServiceProvider sp, Action<string> fail, Action<string> pass)
    {
        var classService = sp.GetRequiredService<IClassService>();
        var studentService = sp.GetRequiredService<IStudentService>();
        var attendanceService = sp.GetRequiredService<IAttendanceService>();

        var sections = await classService.GetSectionOptionsAsync();
        if (sections.Count == 0)
        {
            fail("Pattern report: no sections");
            return;
        }

        var sectionId = sections[0].SectionId;
        const string demoCode = "PATTERN-DEMO";
        var today = DateOnly.FromDateTime(DateTime.Today);
        var from = today.AddDays(-29);

        var existing = await studentService.GetStudentsAsync(search: demoCode, pageSize: 5);
        int studentId;
        if (existing.Items.FirstOrDefault(x => x.StudentCode == demoCode) is { } found)
        {
            studentId = found.Id;
        }
        else
        {
            studentId = await studentService.SaveStudentAsync(new StudentFormDto
            {
                StudentCode = demoCode,
                FirstName = "Pattern",
                LastName = "Demo",
                SectionId = sectionId,
                RollNumber = "PAT-1",
                IsActive = true
            });
        }

        var lateDaysMarked = 0;
        for (var offset = 0; offset < 30 && lateDaysMarked < 5; offset++)
        {
            var date = today.AddDays(-offset);
            var sheet = await attendanceService.GetManualAttendanceSheetAsync(sectionId, date);
            if (!sheet.CanEdit)
            {
                continue;
            }

            var row = sheet.Rows.FirstOrDefault(r => r.StudentId == studentId);
            if (row is null)
            {
                continue;
            }

            row.Status = AttendanceStatus.Late;
            await attendanceService.SaveManualAttendanceAsync(sectionId, date, [row], userId: null);
            lateDaysMarked++;
        }

        if (lateDaysMarked < 3)
        {
            fail($"Pattern report: could only mark {lateDaysMarked} late day(s) for demo student");
            return;
        }

        var report = await attendanceService.GetAttendancePatternAsync(
            from,
            today,
            AttendanceStatus.Late,
            minOccurrences: 3,
            minConsecutive: 0,
            sectionId: null,
            userId: null);

        var demoRow = report.FirstOrDefault(x => x.StudentId == studentId);
        if (demoRow is null)
        {
            fail($"Pattern report: demo student not returned ({report.Count} row(s) total)");
            return;
        }

        pass($"Pattern report: {report.Count} student(s); demo 'Pattern Demo' has {demoRow.OccurrenceCount} late day(s), streak {demoRow.LongestStreak}");
    }

    private static async Task TestBiometricDeviceSaveAsync(IServiceProvider sp, Action<string> fail, Action<string> pass)
    {
        var service = sp.GetRequiredService<IBiometricConfigService>();
        var devices = await service.GetDevicesAsync();
        var suffix = Guid.NewGuid().ToString("N")[..6];
        var id = await service.SaveDeviceAsync(new BiometricDeviceDto
        {
            Id = 0,
            Name = $"QA Device {suffix}",
            IpAddress = "127.0.0.1",
            Port = 4370,
            BiometricType = BiometricType.Fingerprint,
            IsEnabled = true
        });

        var saved = (await service.GetDevicesAsync()).FirstOrDefault(x => x.Id == id);
        if (saved is null || saved.Name != $"QA Device {suffix}")
        {
            fail("Biometric: save device failed");
            return;
        }

        pass($"Biometric: {devices.Count + 1} device(s), save OK");
    }

    private static async Task TestLocalFingerprintAsync(IServiceProvider sp, Action<string> fail, Action<string> pass)
    {
        var classService = sp.GetRequiredService<IClassService>();
        var studentService = sp.GetRequiredService<IStudentService>();
        var localBio = sp.GetRequiredService<ILocalBiometricService>();

        var sections = await classService.GetSectionOptionsAsync();
        var suffix = Guid.NewGuid().ToString("N")[..6];
        var studentId = await studentService.SaveStudentAsync(new StudentFormDto
        {
            StudentCode = $"FP{suffix}",
            FirstName = "Finger",
            LastName = "QA",
            SectionId = sections[0].SectionId,
            RollNumber = $"FP{suffix}",
            IsActive = true
        });

        try
        {
            var credId = $"fp-qa-{suffix}";
            await localBio.EnrollFingerprintAsync(studentId, credId);
            var match = await localBio.MatchFingerprintAsync(credId);
            if (match?.StudentId != studentId)
            {
                fail("Local fingerprint: enroll/match failed");
                return;
            }

            pass("Local fingerprint: enroll/match OK");
        }
        finally
        {
            await studentService.DeleteStudentAsync(studentId);
        }
    }

    private static async Task TestStudentSecondSaveAsync(IServiceProvider sp, Action<string> fail, Action<string> pass)
    {
        var classService = sp.GetRequiredService<IClassService>();
        var studentService = sp.GetRequiredService<IStudentService>();
        var sections = await classService.GetSectionOptionsAsync();
        var sectionId = sections[0].SectionId;
        var suffix = Guid.NewGuid().ToString("N")[..6];

        var id1 = await studentService.SaveStudentAsync(new StudentFormDto
        {
            FirstName = "Second",
            LastName = "SaveA",
            SectionId = sectionId,
            RollNumber = $"A{suffix}",
            IsActive = true
        });

        var id2 = await studentService.SaveStudentAsync(new StudentFormDto
        {
            FirstName = "Second",
            LastName = "SaveB",
            SectionId = sectionId,
            RollNumber = $"B{suffix}",
            IsActive = true
        });

        try
        {
            var s1 = await studentService.GetStudentAsync(id1);
            var s2 = await studentService.GetStudentAsync(id2);
            if (string.IsNullOrWhiteSpace(s1?.StudentCode) || string.IsNullOrWhiteSpace(s2?.StudentCode))
            {
                fail("Student auto-code: empty student code on second save");
                return;
            }

            if (s1.StudentCode == s2.StudentCode)
            {
                fail("Student auto-code: duplicate codes generated");
                return;
            }

            pass($"Student auto-code: {s1.StudentCode}, {s2.StudentCode}");
        }
        finally
        {
            await studentService.DeleteStudentAsync(id1);
            await studentService.DeleteStudentAsync(id2);
        }
    }

    private static async Task TestStudentPhotoAsync(IServiceProvider sp, Action<string> fail, Action<string> pass)
    {
        var classService = sp.GetRequiredService<IClassService>();
        var studentService = sp.GetRequiredService<IStudentService>();
        var sections = await classService.GetSectionOptionsAsync();
        var suffix = Guid.NewGuid().ToString("N")[..6];

        var studentId = await studentService.SaveStudentAsync(new StudentFormDto
        {
            StudentCode = $"PH{suffix}",
            FirstName = "Photo",
            LastName = "Test",
            SectionId = sections[0].SectionId,
            RollNumber = $"PH{suffix}",
            IsActive = true
        });

        try
        {
            await using var stream = new MemoryStream([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00]);
            var path = await studentService.UploadPhotoAsync(studentId, stream, "photo.png");
            var form = await studentService.GetStudentAsync(studentId);

            if (string.IsNullOrWhiteSpace(path) || form?.PhotoPath != path)
            {
                fail("Student photo: upload failed");
                return;
            }

            await studentService.RemovePhotoAsync(studentId);
            form = await studentService.GetStudentAsync(studentId);
            if (!string.IsNullOrWhiteSpace(form?.PhotoPath))
            {
                fail("Student photo: remove failed");
                return;
            }

            pass("Student photo: upload/remove OK");
        }
        finally
        {
            await studentService.DeleteStudentAsync(studentId);
        }
    }

    private static async Task TestUsersManagementAsync(IServiceProvider sp, Action<string> fail, Action<string> pass)
    {
        var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var email = $"qa.user.{suffix}@school.local";

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = "QA User",
            IsActive = true
        };

        var create = await userManager.CreateAsync(user, "QaUser@12345");
        if (!create.Succeeded)
        {
            fail($"Users: create failed — {string.Join(", ", create.Errors.Select(e => e.Description))}");
            return;
        }

        await userManager.AddToRoleAsync(user, "Teacher");
        var roles = await userManager.GetRolesAsync(user);
        user.IsActive = false;
        await userManager.UpdateAsync(user);
        await userManager.DeleteAsync(user);

        if (!roles.Contains("Teacher"))
        {
            fail("Users: role assignment failed");
            return;
        }

        pass("Users: create, role assign, deactivate, delete OK");
    }

    private static async Task TestGatePwaAssetsAsync(
        WebApplicationFactory<Program> factory,
        Action<string> fail,
        Action<string> pass)
    {
        var client = factory.CreateClient();

        var manifestResponse = await client.GetAsync("/manifest.webmanifest");
        if (manifestResponse.StatusCode != HttpStatusCode.OK)
        {
            fail($"PWA manifest: expected 200, got {manifestResponse.StatusCode}");
            return;
        }

        var manifest = await manifestResponse.Content.ReadAsStringAsync();
        if (!manifest.Contains("/attendance/gate", StringComparison.Ordinal))
        {
            fail("PWA manifest: missing gate start_url");
            return;
        }

        if (!manifest.Contains("gate-icon-192.png", StringComparison.Ordinal))
        {
            fail("PWA manifest: missing 192px install icon");
            return;
        }

        var swResponse = await client.GetAsync("/service-worker.js");
        if (swResponse.StatusCode != HttpStatusCode.OK)
        {
            fail($"PWA service worker: expected 200, got {swResponse.StatusCode}");
            return;
        }

        var sw = await swResponse.Content.ReadAsStringAsync();
        if (!sw.Contains("sms-gate-v", StringComparison.Ordinal))
        {
            fail("PWA service worker: unexpected cache name");
            return;
        }

        pass("PWA: manifest and service worker served OK");
    }
}
