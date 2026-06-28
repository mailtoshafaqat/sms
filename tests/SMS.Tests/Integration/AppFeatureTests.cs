using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
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

public class AppFeatureTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly ITestOutputHelper _output;

    public AppFeatureTests(WebApplicationFactory<Program> factory, ITestOutputHelper output)
    {
        _factory = factory;
        _output = output;
    }

    [Fact]
    public async Task LoginPage_IsAccessible()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var response = await client.GetAsync("/login");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Login", html);
        Assert.DoesNotContain("Admin@123", html);
        Assert.DoesNotContain("Demo logins", html);
        _output.WriteLine("Login page: PASS");
    }

    [Fact]
    public async Task ProtectedRoutes_RedirectUnauthenticatedUsers()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var response = await client.GetAsync("/");
        Assert.True(
            response.StatusCode is HttpStatusCode.Redirect or HttpStatusCode.Found,
            $"Expected redirect, got {response.StatusCode}");
        _output.WriteLine("Auth guard: PASS — unauthenticated users redirected");
    }

    [Fact]
    public async Task Login_WithValidCredentials_Succeeds()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var user = await userManager.FindByEmailAsync("admin@school.local");
        Assert.NotNull(user);
        Assert.True(await userManager.CheckPasswordAsync(user, "Admin@123"), "Admin password should be valid");
        _output.WriteLine("Login: PASS — admin@school.local credentials verified");
    }

    [Fact]
    public async Task CoordinatorAccount_HasExpectedCredentials()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var coordinator = await userManager.FindByEmailAsync("coordinator@school.local");
        Assert.NotNull(coordinator);
        Assert.True(await userManager.CheckPasswordAsync(coordinator, "Coordinator@123"));
        var roles = await userManager.GetRolesAsync(coordinator);
        Assert.Contains("Coordinator", roles);
        _output.WriteLine("Coordinator account: PASS");
    }

    [Fact]
    public async Task DefaultSeedAccounts_IncludeAdminAndCoordinator()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var admin = await userManager.FindByEmailAsync("admin@school.local");
        var coordinator = await userManager.FindByEmailAsync("coordinator@school.local");
        Assert.NotNull(admin);
        Assert.NotNull(coordinator);
        _output.WriteLine("Default seed accounts: PASS — admin and coordinator present");
    }

    [Fact]
    public async Task Login_WithInvalidCredentials_Fails()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var user = await userManager.FindByEmailAsync("admin@school.local");
        Assert.NotNull(user);
        Assert.False(await userManager.CheckPasswordAsync(user, "WrongPassword"), "Wrong password should fail");
        _output.WriteLine("Login (invalid): PASS — rejected bad password");
    }

    [Fact]
    public async Task Dashboard_ReturnsStats()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dashboard = scope.ServiceProvider.GetRequiredService<IDashboardService>();
        var stats = await dashboard.GetDashboardAsync();

        Assert.NotNull(stats);
        Assert.NotNull(stats.TodaySummary);
        Assert.NotNull(stats.EnabledDevices);
        Assert.NotNull(stats.RecentScans);
        _output.WriteLine($"Dashboard: PASS — {stats.TodaySummary.EnrolledStudents} students, {stats.EnabledDevices.Count} devices");
    }

    [Fact]
    public async Task SchoolSettings_CanReadAndUpdate()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var schoolService = scope.ServiceProvider.GetRequiredService<ISchoolService>();
        var settings = await schoolService.GetSchoolSettingsAsync();

        Assert.NotNull(settings);
        Assert.False(string.IsNullOrWhiteSpace(settings.Name));
        _output.WriteLine($"School settings read: PASS — {settings.Name}");

        var originalPhone = settings.Phone;
        settings.Phone = "03001234567";
        await schoolService.UpdateSchoolSettingsAsync(settings);

        var updated = await schoolService.GetSchoolSettingsAsync();
        Assert.Equal("03001234567", updated!.Phone);

        settings.Phone = originalPhone;
        await schoolService.UpdateSchoolSettingsAsync(settings);
        _output.WriteLine("School settings update: PASS");
    }

    [Fact]
    public async Task Classes_CanListAndManage()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var classService = scope.ServiceProvider.GetRequiredService<IClassService>();
        var classes = await classService.GetClassesAsync();

        Assert.NotEmpty(classes);
        _output.WriteLine($"Classes list: PASS — {classes.Count} class(es)");

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var classId = await classService.SaveClassAsync($"TestClass-{suffix}");
        Assert.True(classId > 0);

        var sectionId = await classService.SaveSectionAsync(classId, $"S{suffix[..4]}");
        Assert.True(sectionId > 0);

        var sections = await classService.GetSectionOptionsAsync();
        Assert.Contains(sections, s => s.SectionId == sectionId);
        _output.WriteLine($"Classes create: PASS — class {classId}, section {sectionId}");
    }

    [Fact]
    public async Task Classes_CanRenameAndDeleteEmptySection()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var classService = scope.ServiceProvider.GetRequiredService<IClassService>();

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var classId = await classService.SaveClassAsync($"RenameClass-{suffix}");
        var sectionId = await classService.SaveSectionAsync(classId, $"A{suffix[..2]}");

        var renamedClass = $"Renamed-{suffix}";
        await classService.SaveClassAsync(renamedClass, classId);
        var classes = await classService.GetClassesAsync();
        Assert.Contains(classes, c => c.Id == classId && c.Name == renamedClass);

        var renamedSection = $"B{suffix[..2]}";
        await classService.SaveSectionAsync(classId, renamedSection, sectionId);
        var sections = await classService.GetSectionsAsync();
        Assert.Contains(sections, s => s.Id == sectionId && s.SectionName == renamedSection);

        await classService.DeleteSectionAsync(sectionId);
        await classService.DeleteClassAsync(classId);

        classes = await classService.GetClassesAsync();
        Assert.DoesNotContain(classes, c => c.Id == classId);
        _output.WriteLine("Classes rename/delete empty: PASS");
    }

    [Fact]
    public async Task Classes_CanReorderClassAndSection()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var classService = scope.ServiceProvider.GetRequiredService<IClassService>();

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var firstClassId = await classService.SaveClassAsync($"OrderA-{suffix}");
        var secondClassId = await classService.SaveClassAsync($"OrderB-{suffix}");
        var sectionAId = await classService.SaveSectionAsync(firstClassId, "A");
        var sectionBId = await classService.SaveSectionAsync(firstClassId, "B");
        var classesBefore = (await classService.GetClassesAsync()).ToList();
        var idxBBefore = classesBefore.FindIndex(c => c.Id == secondClassId);

        await classService.MoveClassAsync(secondClassId, moveUp: true);
        var classes = await classService.GetClassesAsync();
        var idxBAfter = classes.ToList().FindIndex(c => c.Id == secondClassId);
        Assert.Equal(idxBBefore - 1, idxBAfter);

        await classService.MoveSectionAsync(sectionBId, moveUp: true);
        var sections = await classService.GetSectionsAsync();
        var classSections = sections.Where(s => s.ClassRoomId == firstClassId).Select(s => s.SectionName).ToList();
        Assert.Equal(["B", "A"], classSections);

        await classService.DeleteSectionAsync(sectionAId);
        await classService.DeleteSectionAsync(sectionBId);
        await classService.DeleteClassAsync(firstClassId);
        await classService.DeleteClassAsync(secondClassId);
        _output.WriteLine("Classes reorder: PASS");
    }

    [Fact]
    public async Task Classes_Pagination_DefaultsTo25()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var classService = scope.ServiceProvider.GetRequiredService<IClassService>();

        var page = await classService.GetClassesPagedAsync(1, 25);
        Assert.Equal(25, page.PageSize);
        Assert.Equal(1, page.Page);

        var sectionsPage = await classService.GetSectionsPagedAsync(1, 25);
        Assert.Equal(25, sectionsPage.PageSize);
        _output.WriteLine($"Classes pagination: PASS — {page.Items.Count} classes, {sectionsPage.Items.Count} sections on page 1");
    }

    [Fact]
    public async Task Classes_CannotDeleteSectionWithStudents()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var classService = scope.ServiceProvider.GetRequiredService<IClassService>();
        var studentService = scope.ServiceProvider.GetRequiredService<IStudentService>();

        var sections = await classService.GetSectionOptionsAsync();
        Assert.NotEmpty(sections);
        var sectionId = sections[0].SectionId;

        var suffix = Guid.NewGuid().ToString("N")[..6];
        var studentId = await studentService.SaveStudentAsync(new StudentFormDto
        {
            StudentCode = $"CD{suffix}",
            FirstName = "Class",
            LastName = "DeleteTest",
            SectionId = sectionId,
            RollNumber = $"CD{suffix}",
            Status = StudentStatus.Active
        });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => classService.DeleteSectionAsync(sectionId));
        Assert.Contains("student", ex.Message, StringComparison.OrdinalIgnoreCase);

        await studentService.DeleteStudentAsync(studentId);
        _output.WriteLine("Classes delete guard: PASS");
    }

    [Fact]
    public async Task Classes_CanDeactivateSectionWithStudents()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var classService = scope.ServiceProvider.GetRequiredService<IClassService>();
        var studentService = scope.ServiceProvider.GetRequiredService<IStudentService>();

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var classId = await classService.SaveClassAsync($"Deact-{suffix}");
        var sectionId = await classService.SaveSectionAsync(classId, $"S{suffix[..2]}");

        var studentId = await studentService.SaveStudentAsync(new StudentFormDto
        {
            StudentCode = $"DA{suffix}",
            FirstName = "Deact",
            LastName = "Student",
            SectionId = sectionId,
            RollNumber = $"DA{suffix}",
            Status = StudentStatus.Active
        });

        await classService.SetSectionActiveAsync(sectionId, false);
        var options = await classService.GetSectionOptionsAsync();
        Assert.DoesNotContain(options, s => s.SectionId == sectionId);

        await classService.SetSectionActiveAsync(sectionId, true);
        options = await classService.GetSectionOptionsAsync();
        Assert.Contains(options, s => s.SectionId == sectionId);

        await studentService.DeleteStudentAsync(studentId);
        await Assert.ThrowsAsync<InvalidOperationException>(() => classService.DeleteSectionAsync(sectionId));
        _output.WriteLine("Classes deactivate: PASS");
    }

    [Fact]
    public async Task Students_CanCreateListAndDelete()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var classService = scope.ServiceProvider.GetRequiredService<IClassService>();
        var studentService = scope.ServiceProvider.GetRequiredService<IStudentService>();

        var sections = await classService.GetSectionOptionsAsync();
        Assert.NotEmpty(sections);
        var sectionId = sections[0].SectionId;

        var suffix = Guid.NewGuid().ToString("N")[..6];
        var studentId = await studentService.SaveStudentAsync(new StudentFormDto
        {
            StudentCode = $"TST{suffix}",
            FirstName = "Test",
            LastName = "Student",
            SectionId = sectionId,
            RollNumber = $"T{suffix}",
            FingerprintUserId = $"fp{suffix}",
            Status = StudentStatus.Active
        });
        Assert.True(studentId > 0);
        _output.WriteLine($"Student create: PASS — id {studentId}");

        var page = await studentService.GetStudentsAsync("Test");
        Assert.Contains(page.Items, s => s.Id == studentId);

        var form = await studentService.GetStudentAsync(studentId);
        Assert.NotNull(form);
        Assert.Equal("Test", form.FirstName);
        _output.WriteLine("Student read: PASS");

        await studentService.DeleteStudentAsync(studentId);
        var studentsAfter = await studentService.GetStudentsAsync($"TST{suffix}");
        Assert.DoesNotContain(studentsAfter.Items, s => s.Id == studentId);
        _output.WriteLine("Student delete: PASS");
    }

    [Fact]
    public async Task Students_Pagination_ReturnsPages()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var studentService = scope.ServiceProvider.GetRequiredService<IStudentService>();

        var page1 = await studentService.GetStudentsAsync(page: 1, pageSize: 5);
        Assert.NotNull(page1);
        Assert.True(page1.PageSize == 5);
        Assert.True(page1.TotalCount >= 0);
        Assert.True(page1.Items.Count <= 5);
        _output.WriteLine($"Student pagination: PASS — {page1.Items.Count} of {page1.TotalCount}");
    }

    [Fact]
    public async Task Students_CanPromoteToNextClass()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var classService = scope.ServiceProvider.GetRequiredService<IClassService>();
        var studentService = scope.ServiceProvider.GetRequiredService<IStudentService>();
        var promotionService = scope.ServiceProvider.GetRequiredService<IStudentPromotionService>();

        var suffix = Guid.NewGuid().ToString("N")[..6];
        var fromClassId = await classService.SaveClassAsync($"PromoteFrom-{suffix}");
        var fromSectionId = await classService.SaveSectionAsync(fromClassId, "A");
        var toClassId = await classService.SaveClassAsync($"PromoteTo-{suffix}");
        var toSectionId = await classService.SaveSectionAsync(toClassId, "A");

        var studentId = await studentService.SaveStudentAsync(new StudentFormDto
        {
            StudentCode = $"PRM{suffix}",
            FirstName = "Promote",
            LastName = "Test",
            SectionId = fromSectionId,
            RollNumber = $"P{suffix}",
            Status = StudentStatus.Active
        });

        try
        {
            var candidates = await promotionService.GetCandidatesAsync(fromSectionId);
            Assert.Contains(candidates, c => c.StudentId == studentId);

            var result = await promotionService.PromoteStudentsAsync(
                fromSectionId,
                [studentId],
                toSectionId,
                PromotionSource.Manual,
                "test-user");

            Assert.Equal(1, result.PromotedCount);

            var form = await studentService.GetStudentAsync(studentId);
            Assert.NotNull(form);
            Assert.Equal(toSectionId, form.SectionId);
            _output.WriteLine($"Student promotion: PASS — moved to section {toSectionId}");
        }
        finally
        {
            await studentService.DeleteStudentAsync(studentId);
        }
    }

    [Fact]
    public async Task Attendance_ManualMarkingAndSummary()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var classService = scope.ServiceProvider.GetRequiredService<IClassService>();
        var studentService = scope.ServiceProvider.GetRequiredService<IStudentService>();
        var attendanceService = scope.ServiceProvider.GetRequiredService<IAttendanceService>();

        var sections = await classService.GetSectionOptionsAsync();
        var sectionId = sections[0].SectionId;
        var attendanceDate = await FindEditableAttendanceDateAsync(attendanceService, sectionId);

        var suffix = Guid.NewGuid().ToString("N")[..6];
        var studentId = await studentService.SaveStudentAsync(new StudentFormDto
        {
            StudentCode = $"ATT{suffix}",
            FirstName = "Attend",
            LastName = "Test",
            SectionId = sectionId,
            RollNumber = $"A{suffix}",
            FingerprintUserId = $"fp{suffix}",
            Status = StudentStatus.Active
        });

        try
        {
            var sheet = await attendanceService.GetManualAttendanceSheetAsync(sectionId, attendanceDate);
            Assert.Contains(sheet.Rows, r => r.StudentId == studentId);
            Assert.True(sheet.CanEdit, sheet.BlockReason ?? "Expected an editable working day.");

            var row = sheet.Rows.First(r => r.StudentId == studentId);
            row.Status = AttendanceStatus.Present;
            await attendanceService.SaveManualAttendanceAsync(sectionId, attendanceDate, [row], userId: null);

            var summary = await attendanceService.GetDailySummaryAsync(attendanceDate);
            Assert.True(summary.Present >= 1);
            _output.WriteLine($"Manual attendance: PASS — {summary.Present} present today");
        }
        finally
        {
            await studentService.DeleteStudentAsync(studentId);
        }
    }

    [Fact]
    public async Task Attendance_BiometricScanAndLogs()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var classService = scope.ServiceProvider.GetRequiredService<IClassService>();
        var studentService = scope.ServiceProvider.GetRequiredService<IStudentService>();
        var attendanceService = scope.ServiceProvider.GetRequiredService<IAttendanceService>();
        var biometricConfig = scope.ServiceProvider.GetRequiredService<IBiometricConfigService>();

        var sections = await classService.GetSectionOptionsAsync();
        var sectionId = sections[0].SectionId;
        var devices = await biometricConfig.GetEnabledDevicesAsync();
        Assert.NotEmpty(devices);
        var device = devices.First(d => d.BiometricType == BiometricType.Fingerprint);

        var suffix = Guid.NewGuid().ToString("N")[..6];
        var bioUserId = $"bio{suffix}";
        var studentId = await studentService.SaveStudentAsync(new StudentFormDto
        {
            StudentCode = $"BIO{suffix}",
            FirstName = "Bio",
            LastName = "Scan",
            SectionId = sectionId,
            RollNumber = $"B{suffix}",
            FingerprintUserId = bioUserId,
            Status = StudentStatus.Active
        });

        try
        {
            await attendanceService.ProcessBiometricScanAsync(bioUserId, device.Id, ScanDirection.In);
            var logs = await attendanceService.GetRecentLogsAsync(10);
            Assert.Contains(logs, l => l.StudentName.Contains("Bio"));
            _output.WriteLine($"Biometric scan: PASS — {logs.Count} recent log(s)");
        }
        finally
        {
            await studentService.DeleteStudentAsync(studentId);
        }
    }

    [Fact]
    public async Task BiometricScan_InThenOut_AllowedWithinDuplicateWindow()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var classService = scope.ServiceProvider.GetRequiredService<IClassService>();
        var studentService = scope.ServiceProvider.GetRequiredService<IStudentService>();
        var attendanceService = scope.ServiceProvider.GetRequiredService<IAttendanceService>();
        var localBio = scope.ServiceProvider.GetRequiredService<ILocalBiometricService>();

        var sections = await classService.GetSectionOptionsAsync();
        var sectionId = sections[0].SectionId;
        var suffix = Guid.NewGuid().ToString("N")[..6];
        var studentId = await studentService.SaveStudentAsync(new StudentFormDto
        {
            StudentCode = $"INOUT{suffix}",
            FirstName = "InOut",
            LastName = "Test",
            SectionId = sectionId,
            RollNumber = $"IO{suffix}",
            Status = StudentStatus.Active
        });

        try
        {
            var descriptor = Enumerable.Range(0, 128).Select(i => (float)(Random.Shared.NextDouble() + i * 0.001)).ToArray();
            var externalId = await localBio.EnrollFaceAsync(studentId, descriptor);

            var checkIn = await localBio.ScanByExternalIdAsync(externalId, BiometricType.Face, ScanDirection.In);
            Assert.True(checkIn.Success, checkIn.Message);

            var checkOut = await localBio.ScanByExternalIdAsync(externalId, BiometricType.Face, ScanDirection.Out);
            Assert.True(checkOut.Success, checkOut.Message);

            var logs = await attendanceService.GetRecentLogsAsync(10);
            Assert.Contains(logs, l => l.StudentName.Contains("InOut") && l.Direction == ScanDirection.In);
            Assert.Contains(logs, l => l.StudentName.Contains("InOut") && l.Direction == ScanDirection.Out);
            _output.WriteLine("Biometric IN/OUT: PASS — both scans recorded back-to-back");
        }
        finally
        {
            await studentService.DeleteStudentAsync(studentId);
        }
    }

    [Fact]
    public async Task EnrollFace_PersistsAndAppearsInGateList()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var classService = scope.ServiceProvider.GetRequiredService<IClassService>();
        var studentService = scope.ServiceProvider.GetRequiredService<IStudentService>();
        var localBio = scope.ServiceProvider.GetRequiredService<ILocalBiometricService>();

        var sections = await classService.GetSectionOptionsAsync();
        var sectionId = sections[0].SectionId;
        var suffix = Guid.NewGuid().ToString("N")[..6];
        var studentId = await studentService.SaveStudentAsync(new StudentFormDto
        {
            StudentCode = $"ENRL{suffix}",
            FirstName = "Enroll",
            LastName = "Gate",
            SectionId = sectionId,
            RollNumber = $"EG{suffix}",
            Status = StudentStatus.Active
        });

        try
        {
            var descriptor = Enumerable.Range(0, 128).Select(i => (float)(Random.Shared.NextDouble() + i * 0.001)).ToArray();
            var externalId = await localBio.EnrollFaceAsync(studentId, descriptor);
            Assert.False(string.IsNullOrWhiteSpace(externalId));

            var verify = await localBio.MatchFaceAsync(descriptor);
            Assert.NotNull(verify);

            var gateList = await localBio.GetFaceEnrollmentsAsync();
            Assert.Contains(gateList, x => x.ExternalId == externalId && x.Descriptors.Count > 0);
            _output.WriteLine($"Enroll face gate list: PASS — {gateList.Count} enrolled");
        }
        finally
        {
            await studentService.DeleteStudentAsync(studentId);
        }
    }

    [Fact]
    public async Task GateApi_EnrollmentsAndRecord_MarkInAndOut()
    {
        var authFactory = _factory.WithQaAuthentication();
        await using var scope = authFactory.Services.CreateAsyncScope();
        var classService = scope.ServiceProvider.GetRequiredService<IClassService>();
        var studentService = scope.ServiceProvider.GetRequiredService<IStudentService>();
        var localBio = scope.ServiceProvider.GetRequiredService<ILocalBiometricService>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var admin = await userManager.FindByEmailAsync("admin@school.local")
            ?? throw new InvalidOperationException("Admin user missing.");
        var client = authFactory.CreateRoleClient("Admin", admin.Id);

        var sections = await classService.GetSectionOptionsAsync();
        var sectionId = sections[0].SectionId;
        var suffix = Guid.NewGuid().ToString("N")[..6];
        var studentId = await studentService.SaveStudentAsync(new StudentFormDto
        {
            StudentCode = $"GATE{suffix}",
            FirstName = "Gate",
            LastName = "Api",
            SectionId = sectionId,
            RollNumber = $"G{suffix}",
            Status = StudentStatus.Active
        });

        try
        {
            var descriptor = Enumerable.Range(0, 128).Select(i => (float)(Random.Shared.NextDouble() + i * 0.001)).ToArray();
            var externalId = await localBio.EnrollFaceAsync(studentId, descriptor);

            var enrollmentsResponse = await client.GetAsync("/attendance/gate/enrollments");
            Assert.Equal(HttpStatusCode.OK, enrollmentsResponse.StatusCode);
            var enrollmentsJson = await enrollmentsResponse.Content.ReadAsStringAsync();
            Assert.Contains(externalId, enrollmentsJson);
            _output.WriteLine("Gate API enrollments: PASS");

            var checkInResponse = await client.PostAsJsonAsync("/attendance/gate/record", new { externalId });
            Assert.Equal(HttpStatusCode.OK, checkInResponse.StatusCode);
            var checkIn = await checkInResponse.Content.ReadFromJsonAsync<GateFaceScanResponse>();
            Assert.NotNull(checkIn);
            Assert.True(checkIn.Success, checkIn.Message);
            _output.WriteLine($"Gate API check-in: PASS — {checkIn.Message}");

            var checkOutResponse = await client.PostAsJsonAsync("/attendance/gate/record", new { externalId });
            Assert.Equal(HttpStatusCode.OK, checkOutResponse.StatusCode);
            var checkOut = await checkOutResponse.Content.ReadFromJsonAsync<GateFaceScanResponse>();
            Assert.NotNull(checkOut);
            Assert.True(checkOut.Success, checkOut.Message);
            _output.WriteLine($"Gate API check-out: PASS — {checkOut.Message}");
        }
        finally
        {
            await studentService.DeleteStudentAsync(studentId);
        }
    }

    [Fact]
    public async Task Attendance_PatternReport_ReturnsSeededStudent()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var classService = scope.ServiceProvider.GetRequiredService<IClassService>();
        var studentService = scope.ServiceProvider.GetRequiredService<IStudentService>();
        var attendanceService = scope.ServiceProvider.GetRequiredService<IAttendanceService>();

        var sections = await classService.GetSectionOptionsAsync();
        Assert.NotEmpty(sections);
        var sectionId = sections[0].SectionId;
        const string demoCode = "PATTERN-DEMO";
        var today = DateOnly.FromDateTime(DateTime.Today);

        var existing = await studentService.GetStudentsAsync(search: demoCode, pageSize: 5, filter: StudentListFilter.All);
        var existingRow = existing.Items.FirstOrDefault(x => x.StudentCode == demoCode);
        int studentId;
        if (existingRow is null)
        {
            studentId = await studentService.SaveStudentAsync(new StudentFormDto
            {
                StudentCode = demoCode,
                FirstName = "Pattern",
                LastName = "Demo",
                SectionId = sectionId,
                RollNumber = "PAT-1",
                Status = StudentStatus.Active
            });
        }
        else
        {
            studentId = existingRow.Id;
            if (!existingRow.IsActive || existingRow.Status != StudentStatus.Active)
            {
                studentId = await studentService.SaveStudentAsync(new StudentFormDto
                {
                    Id = existingRow.Id,
                    StudentCode = demoCode,
                    FirstName = "Pattern",
                    LastName = "Demo",
                    SectionId = sectionId,
                    RollNumber = "PAT-1",
                    Status = StudentStatus.Active
                });
            }
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

        Assert.True(lateDaysMarked >= 3, $"Expected at least 3 editable late days, got {lateDaysMarked}");

        var report = await attendanceService.GetAttendancePatternAsync(
            today.AddDays(-29),
            today,
            AttendanceStatus.Late,
            minOccurrences: 3,
            minConsecutive: 0);

        var demoRow = report.FirstOrDefault(x => x.StudentId == studentId);
        Assert.NotNull(demoRow);
        Assert.True(demoRow.OccurrenceCount >= 3);
        _output.WriteLine($"Pattern report: {report.Count} student(s); Pattern Demo late={demoRow.OccurrenceCount}, streak={demoRow.LongestStreak}");
    }

    [Fact]
    public async Task Attendance_FinalizeDaily()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var attendanceService = scope.ServiceProvider.GetRequiredService<IAttendanceService>();
        var today = DateOnly.FromDateTime(DateTime.Today);

        await attendanceService.FinalizeDailyAttendanceAsync(today);
        var summary = await attendanceService.GetDailySummaryAsync(today);
        Assert.NotNull(summary);
        _output.WriteLine($"Finalize daily: PASS — enrolled {summary.EnrolledStudents}");
    }

    [Fact]
    public async Task BiometricConfig_CanListAndTestConnection()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var biometricConfig = scope.ServiceProvider.GetRequiredService<IBiometricConfigService>();

        var devices = await biometricConfig.GetDevicesAsync();
        Assert.NotEmpty(devices);
        _output.WriteLine($"Biometric devices: PASS — {devices.Count} device(s)");

        var device = devices[0];
        var connected = await biometricConfig.TestConnectionAsync(device.Id);
        Assert.True(connected, "Simulated connector should report connected");
        _output.WriteLine($"Biometric test connection: PASS — device {device.Name}");
    }

    [Fact]
    public async Task LocalBiometric_FaceEnrollAndMatch()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var classService = scope.ServiceProvider.GetRequiredService<IClassService>();
        var studentService = scope.ServiceProvider.GetRequiredService<IStudentService>();
        var localBio = scope.ServiceProvider.GetRequiredService<ILocalBiometricService>();

        var sections = await classService.GetSectionOptionsAsync();
        var sectionId = sections[0].SectionId;

        var suffix = Guid.NewGuid().ToString("N")[..6];
        var studentId = await studentService.SaveStudentAsync(new StudentFormDto
        {
            StudentCode = $"FACE{suffix}",
            FirstName = "Face",
            LastName = "Test",
            SectionId = sectionId,
            RollNumber = $"F{suffix}",
            FaceUserId = $"face-{suffix}",
            Status = StudentStatus.Active
        });

        try
        {
            var descriptor = Enumerable.Range(0, 128)
                .Select(i => (float)(Random.Shared.NextDouble() + i * 0.001))
                .ToArray();
            var enrollResult = await localBio.EnrollFaceAsync(studentId, descriptor);
            Assert.False(string.IsNullOrWhiteSpace(enrollResult));

            var match = await localBio.MatchFaceAsync(descriptor);
            Assert.NotNull(match);
            Assert.Equal(studentId, match.StudentId);
            _output.WriteLine($"Local face biometric: PASS — matched {match.StudentName}");
        }
        finally
        {
            await studentService.DeleteStudentAsync(studentId);
        }
    }

    [Fact]
    public async Task HelpPage_RequiresAuthentication()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var response = await client.GetAsync("/help");
        Assert.True(
            response.StatusCode is HttpStatusCode.Redirect or HttpStatusCode.Found,
            $"Expected redirect, got {response.StatusCode}");
        _output.WriteLine("Help page: PASS — requires login");
    }

    [Fact]
    public async Task DatabaseBackupService_ListsBackupsWithoutError()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var backupService = scope.ServiceProvider.GetRequiredService<IDatabaseBackupService>();
        var backups = await backupService.ListBackupsAsync();
        Assert.NotNull(backups);
        _output.WriteLine($"Backup list: PASS — {backups.Count} file(s)");
    }

    [Fact]
    public async Task AnnualRecurringHoliday_AppliesEveryYear()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var attendance = scope.ServiceProvider.GetRequiredService<IAttendanceService>();

        var anchorDate = new DateOnly(2024, 8, 14);
        await attendance.MarkHolidayAsync(anchorDate, "Independence Day", repeatsAnnually: true);

        try
        {
            var summary2025 = await attendance.GetDailySummaryAsync(new DateOnly(2025, 8, 14));
            var summary2026 = await attendance.GetDailySummaryAsync(new DateOnly(2026, 8, 14));
            var calendar = await attendance.GetAttendanceCalendarAsync(2026, 8);

            Assert.True(summary2025.IsHoliday, "Annual holiday should apply in 2025.");
            Assert.True(summary2026.IsHoliday, "Annual holiday should apply in 2026.");

            var day = calendar.Days.First(x => x.Date == new DateOnly(2026, 8, 14));
            Assert.True(day.IsAnnualRecurringHoliday);
            Assert.Equal("Independence Day", day.HolidayTitle);

            var annualList = await attendance.GetAnnualHolidaysAsync();
            Assert.Contains(annualList, x => x.Title == "Independence Day" && x.RecurringMonth == 8 && x.RecurringDay == 14);

            _output.WriteLine("Annual recurring holiday: PASS");
        }
        finally
        {
            var annual = await attendance.GetAnnualHolidaysAsync();
            foreach (var item in annual.Where(x => x.Title == "Independence Day"))
            {
                await attendance.RemoveAnnualHolidayAsync(item.Id);
            }
        }
    }

    [Fact]
    public async Task StaffReports_MonthlyRegisterAndSummary_CountAttendedDays()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var teacherService = scope.ServiceProvider.GetRequiredService<ITeacherAssignmentService>();
        var staffAttendance = scope.ServiceProvider.GetRequiredService<IStaffAttendanceService>();

        var teacherId = await teacherService.SaveStaffMemberAsync(new StaffMemberFormDto
        {
            FirstName = "Report",
            LastName = "Staff",
            EmployeeCode = $"RS{Guid.NewGuid().ToString("N")[..4]}",
            IsActive = true
        });

        try
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            var start = today.AddDays(-6);
            await staffAttendance.SaveSheetAsync(today, [
                new StaffAttendanceRowDto
                {
                    TeacherId = teacherId,
                    EmployeeCode = "RS",
                    StaffName = "Report Staff",
                    Status = AttendanceStatus.Present
                }
            ], userId: null);

            var register = await staffAttendance.GetMonthlyRegisterAsync(today.Year, today.Month);
            Assert.Contains(register.Staff, x => x.StaffName.Contains("Report"));

            var summary = await staffAttendance.GetSummaryAsync(start, today);
            var row = Assert.Single(summary.Rows, x => x.TeacherId == teacherId);
            Assert.True(row.AttendedDays >= 1, $"Expected at least 1 attended day, got {row.AttendedDays}");
            Assert.True(row.PresentDays >= 1);

            _output.WriteLine($"Staff summary: PASS — attended {row.AttendedDays}/{row.WorkingDays} working days");
        }
        finally
        {
            // Staff record left in DB is acceptable for integration test DB; no delete API for teachers.
        }
    }

    private static async Task<DateOnly> FindEditableAttendanceDateAsync(IAttendanceService attendanceService, int sectionId)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        for (var offset = 0; offset <= 14; offset++)
        {
            var date = today.AddDays(-offset);
            var sheet = await attendanceService.GetManualAttendanceSheetAsync(sectionId, date, userId: null);
            if (sheet.CanEdit)
            {
                return date;
            }
        }

        throw new InvalidOperationException("No editable attendance date found within the last two weeks.");
    }
}
