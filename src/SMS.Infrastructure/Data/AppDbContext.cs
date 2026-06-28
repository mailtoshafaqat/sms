using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SMS.Domain.Entities.Attendance;
using SMS.Domain.Entities.Shared;
using SMS.Infrastructure.Identity;

namespace SMS.Infrastructure.Data;

public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<School> Schools => Set<School>();
    public DbSet<AcademicYear> AcademicYears => Set<AcademicYear>();
    public DbSet<ClassRoom> ClassRooms => Set<ClassRoom>();
    public DbSet<Section> Sections => Set<Section>();
    public DbSet<Student> Students => Set<Student>();
    public DbSet<StudentEnrollment> StudentEnrollments => Set<StudentEnrollment>();
    public DbSet<StudentPromotion> StudentPromotions => Set<StudentPromotion>();
    public DbSet<Teacher> Teachers => Set<Teacher>();
    public DbSet<BiometricDevice> BiometricDevices => Set<BiometricDevice>();
    public DbSet<StudentBiometricMap> StudentBiometricMaps => Set<StudentBiometricMap>();
    public DbSet<AttendanceLog> AttendanceLogs => Set<AttendanceLog>();
    public DbSet<DailyAttendance> DailyAttendances => Set<DailyAttendance>();
    public DbSet<StudentLocalTemplate> StudentLocalTemplates => Set<StudentLocalTemplate>();
    public DbSet<SchoolHoliday> SchoolHolidays => Set<SchoolHoliday>();
    public DbSet<AttendanceNotificationLog> AttendanceNotificationLogs => Set<AttendanceNotificationLog>();
    public DbSet<StaffDailyAttendance> StaffDailyAttendances => Set<StaffDailyAttendance>();
    public DbSet<AppExceptionLog> AppExceptionLogs => Set<AppExceptionLog>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}

