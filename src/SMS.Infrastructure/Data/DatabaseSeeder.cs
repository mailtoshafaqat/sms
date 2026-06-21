using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SMS.Domain.Common;
using SMS.Domain.Entities.Attendance;
using SMS.Domain.Entities.Shared;
using SMS.Domain.Enums;
using SMS.Infrastructure.Data;
using SMS.Infrastructure.Identity;

namespace SMS.Infrastructure.Data;

public static class DatabaseSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        await context.Database.MigrateAsync();
        await FixBiometricTypeDefaultsAsync(context);

        foreach (var role in new[] { "Admin", "Coordinator", "Teacher" })
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        if (!await context.Schools.AnyAsync())
        {
            var school = new School
            {
                Name = "Demo School",
                Address = "Main Campus",
                Phone = "03000000000",
                SchoolStartTime = new TimeOnly(8, 0),
                LateAfterMinutes = 60,
                SchoolEndTime = new TimeOnly(13, 0),
                WeeklyOffDays = WeeklyOffDays.Sunday
            };

            var year = new AcademicYear
            {
                Name = $"{DateTime.Now.Year}-{DateTime.Now.Year + 1}",
                StartDate = new DateOnly(DateTime.Now.Year, 4, 1),
                EndDate = new DateOnly(DateTime.Now.Year + 1, 3, 31),
                IsCurrent = true
            };

            school.AcademicYears.Add(year);

            var classNine = new ClassRoom { Name = "Class 9", DisplayOrder = 9 };
            var sectionA = new Section { Name = "A" };
            classNine.Sections.Add(sectionA);
            school.Classes.Add(classNine);

            school.BiometricDevices.Add(new BiometricDevice
            {
                Name = "Gate Finger",
                BiometricType = BiometricType.Fingerprint,
                IsEnabled = true
            });

            school.BiometricDevices.Add(new BiometricDevice
            {
                Name = "Gate Face",
                BiometricType = BiometricType.Face,
                IsEnabled = true
            });

            context.Schools.Add(school);
            await context.SaveChangesAsync();
        }

        if (await userManager.FindByEmailAsync("admin@school.local") is null)
        {
            var admin = new ApplicationUser
            {
                UserName = "admin@school.local",
                Email = "admin@school.local",
                EmailConfirmed = true,
                DisplayName = "System Admin",
                IsActive = true
            };

            await userManager.CreateAsync(admin, "Admin@123");
            await userManager.AddToRoleAsync(admin, "Admin");
        }

        if (await userManager.FindByEmailAsync("teacher@school.local") is null)
        {
            var teacher = new ApplicationUser
            {
                UserName = "teacher@school.local",
                Email = "teacher@school.local",
                EmailConfirmed = true,
                DisplayName = "Demo Teacher",
                IsActive = true
            };

            await userManager.CreateAsync(teacher, "Teacher@123");
            await userManager.AddToRoleAsync(teacher, "Teacher");
        }

        if (await userManager.FindByEmailAsync("coordinator@school.local") is null)
        {
            var coordinator = new ApplicationUser
            {
                UserName = "coordinator@school.local",
                Email = "coordinator@school.local",
                EmailConfirmed = true,
                DisplayName = "Attendance Coordinator",
                IsActive = true
            };

            await userManager.CreateAsync(coordinator, "Coordinator@123");
            await userManager.AddToRoleAsync(coordinator, "Coordinator");
        }

        await EnsureDemoTeacherSectionLinkAsync(context, userManager);
    }

    private static async Task EnsureDemoTeacherSectionLinkAsync(AppDbContext context, UserManager<ApplicationUser> userManager)
    {
        var teacherUser = await userManager.FindByEmailAsync("teacher@school.local");
        if (teacherUser is null)
        {
            return;
        }

        var school = await context.Schools
            .Include(x => x.Classes)
            .ThenInclude(x => x.Sections)
            .FirstOrDefaultAsync();

        if (school is null)
        {
            return;
        }

        var section = school.Classes.SelectMany(x => x.Sections).FirstOrDefault();
        if (section is null)
        {
            return;
        }

        var teacher = await context.Teachers.FirstOrDefaultAsync(x => x.UserId == teacherUser.Id);
        if (teacher is null)
        {
            teacher = new Teacher
            {
                SchoolId = school.Id,
                UserId = teacherUser.Id,
                FirstName = "Demo",
                LastName = "Teacher",
                EmployeeCode = "T001",
                IsActive = true
            };
            context.Teachers.Add(teacher);
            await context.SaveChangesAsync();
        }

        if (section.ClassTeacherId != teacher.Id)
        {
            section.ClassTeacherId = teacher.Id;
            await context.SaveChangesAsync();
        }
    }

    private static async Task FixBiometricTypeDefaultsAsync(AppDbContext context)
    {
        await context.Database.ExecuteSqlRawAsync("""
            UPDATE attendance.BiometricDevices SET Modality = 1 WHERE Modality = 0;
            UPDATE attendance.StudentBiometricMaps SET Modality = 1 WHERE Modality = 0;
            UPDATE attendance.AttendanceLogs SET ScanModality = 1 WHERE ScanModality = 0;
            """);

        var school = await context.Schools
            .Include(x => x.BiometricDevices)
            .FirstOrDefaultAsync();

        if (school is null)
        {
            return;
        }

        if (!school.BiometricDevices.Any(x => x.BiometricType == BiometricType.Fingerprint))
        {
            context.BiometricDevices.Add(new BiometricDevice
            {
                SchoolId = school.Id,
                Name = "Gate Finger",
                BiometricType = BiometricType.Fingerprint,
                IsEnabled = true
            });
        }

        if (!school.BiometricDevices.Any(x => x.BiometricType == BiometricType.Face))
        {
            context.BiometricDevices.Add(new BiometricDevice
            {
                SchoolId = school.Id,
                Name = "Gate Face",
                BiometricType = BiometricType.Face,
                IsEnabled = true
            });
        }

        foreach (var device in school.BiometricDevices)
        {
            device.Name = device.Name switch
            {
                "Main Gate" => "Gate Finger",
                "Main Gate Fingerprint" => "Gate Finger",
                "Main Gate Face" => "Gate Face",
                _ => device.Name
            };
        }

        await context.SaveChangesAsync();
    }
}

