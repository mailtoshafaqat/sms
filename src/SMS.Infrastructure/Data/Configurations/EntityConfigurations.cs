using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMS.Domain.Entities.Attendance;
using SMS.Domain.Entities.Shared;

namespace SMS.Infrastructure.Data.Configurations;

public class SchoolConfiguration : IEntityTypeConfiguration<School>
{
    public void Configure(EntityTypeBuilder<School> builder)
    {
        builder.ToTable("Schools", "shared");
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Phone).HasMaxLength(30);
        builder.Property(x => x.WhatsAppNumber).HasMaxLength(30);
        builder.Property(x => x.Email).HasMaxLength(100);
        builder.Property(x => x.LogoPath).HasMaxLength(300);
        builder.Property(x => x.AbsentNotificationTemplate).HasMaxLength(500);
        builder.Property(x => x.LateNotificationTemplate).HasMaxLength(500);
        builder.Property(x => x.CheckInNotificationTemplate).HasMaxLength(500);
        builder.Property(x => x.CheckOutNotificationTemplate).HasMaxLength(500);
        builder.Property(x => x.LeaveNotificationTemplate).HasMaxLength(500);
        builder.Property(x => x.PresentNotificationTemplate).HasMaxLength(500);
    }
}

public class AcademicYearConfiguration : IEntityTypeConfiguration<AcademicYear>
{
    public void Configure(EntityTypeBuilder<AcademicYear> builder)
    {
        builder.ToTable("AcademicYears", "shared");
        builder.Property(x => x.Name).HasMaxLength(50).IsRequired();
        builder.HasIndex(x => new { x.SchoolId, x.IsCurrent });
    }
}

public class ClassRoomConfiguration : IEntityTypeConfiguration<ClassRoom>
{
    public void Configure(EntityTypeBuilder<ClassRoom> builder)
    {
        builder.ToTable("ClassRooms", "shared");
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.HasIndex(x => new { x.SchoolId, x.Name }).IsUnique();
    }
}

public class SectionConfiguration : IEntityTypeConfiguration<Section>
{
    public void Configure(EntityTypeBuilder<Section> builder)
    {
        builder.ToTable("Sections", "shared");
        builder.Property(x => x.Name).HasMaxLength(20).IsRequired();
        builder.HasOne(x => x.ClassRoom)
            .WithMany(x => x.Sections)
            .HasForeignKey(x => x.ClassRoomId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ClassTeacher)
            .WithMany()
            .HasForeignKey(x => x.ClassTeacherId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}

public class StudentConfiguration : IEntityTypeConfiguration<Student>
{
    public void Configure(EntityTypeBuilder<Student> builder)
    {
        builder.ToTable("Students", "shared");
        builder.Property(x => x.StudentCode).HasMaxLength(50).IsRequired();
        builder.Property(x => x.FirstName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.LastName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.PhotoPath).HasMaxLength(300);
        builder.Property(x => x.StatusNote).HasMaxLength(500);
        builder.HasIndex(x => new { x.SchoolId, x.StudentCode }).IsUnique();
    }
}

public class StudentEnrollmentConfiguration : IEntityTypeConfiguration<StudentEnrollment>
{
    public void Configure(EntityTypeBuilder<StudentEnrollment> builder)
    {
        builder.ToTable("StudentEnrollments", "shared");
        builder.Property(x => x.RollNumber).HasMaxLength(20).IsRequired();
        builder.HasIndex(x => new { x.AcademicYearId, x.SectionId, x.RollNumber }).IsUnique();
        builder.HasIndex(x => new { x.StudentId, x.AcademicYearId }).IsUnique();
        builder.HasOne(x => x.Student)
            .WithMany(x => x.Enrollments)
            .HasForeignKey(x => x.StudentId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.AcademicYear)
            .WithMany(x => x.Enrollments)
            .HasForeignKey(x => x.AcademicYearId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Section)
            .WithMany(x => x.Enrollments)
            .HasForeignKey(x => x.SectionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class StudentPromotionConfiguration : IEntityTypeConfiguration<StudentPromotion>
{
    public void Configure(EntityTypeBuilder<StudentPromotion> builder)
    {
        builder.ToTable("StudentPromotions", "shared");
        builder.Property(x => x.Notes).HasMaxLength(500);
        builder.Property(x => x.PromotedByUserId).HasMaxLength(450);
        builder.HasIndex(x => new { x.StudentId, x.PromotedAt });
        builder.HasOne(x => x.Student)
            .WithMany()
            .HasForeignKey(x => x.StudentId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.AcademicYear)
            .WithMany()
            .HasForeignKey(x => x.AcademicYearId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.FromSection)
            .WithMany()
            .HasForeignKey(x => x.FromSectionId)
            .OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.ToSection)
            .WithMany()
            .HasForeignKey(x => x.ToSectionId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}

public class TeacherConfiguration : IEntityTypeConfiguration<Teacher>
{
    public void Configure(EntityTypeBuilder<Teacher> builder)
    {
        builder.ToTable("Teachers", "shared");
        builder.Property(x => x.EmployeeCode).HasMaxLength(50).IsRequired();
        builder.Property(x => x.FirstName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.LastName).HasMaxLength(100).IsRequired();
    }
}

public class BiometricDeviceConfiguration : IEntityTypeConfiguration<BiometricDevice>
{
    public void Configure(EntityTypeBuilder<BiometricDevice> builder)
    {
        builder.ToTable("BiometricDevices", "attendance");
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.Property(x => x.IpAddress).HasMaxLength(50);
        builder.Property(x => x.ComPort).HasMaxLength(20);
        builder.HasOne(x => x.School)
            .WithMany(x => x.BiometricDevices)
            .HasForeignKey(x => x.SchoolId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class StudentBiometricMapConfiguration : IEntityTypeConfiguration<StudentBiometricMap>
{
    public void Configure(EntityTypeBuilder<StudentBiometricMap> builder)
    {
        builder.ToTable("StudentBiometricMaps", "attendance");
        builder.Property(x => x.BiometricUserId).HasMaxLength(50).IsRequired();
        builder.HasIndex(x => new { x.BiometricDeviceId, x.BiometricUserId }).IsUnique();
        builder.HasOne(x => x.Student)
            .WithMany(x => x.BiometricMaps)
            .HasForeignKey(x => x.StudentId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.BiometricDevice)
            .WithMany(x => x.BiometricMaps)
            .HasForeignKey(x => x.BiometricDeviceId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class AttendanceLogConfiguration : IEntityTypeConfiguration<AttendanceLog>
{
    public void Configure(EntityTypeBuilder<AttendanceLog> builder)
    {
        builder.ToTable("AttendanceLogs", "attendance");
        builder.Property(x => x.Source).HasMaxLength(50);
        builder.HasIndex(x => new { x.StudentId, x.AttendanceDate, x.ScanTime });
        builder.HasOne(x => x.Student)
            .WithMany()
            .HasForeignKey(x => x.StudentId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.BiometricDevice)
            .WithMany(x => x.AttendanceLogs)
            .HasForeignKey(x => x.BiometricDeviceId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public class DailyAttendanceConfiguration : IEntityTypeConfiguration<DailyAttendance>
{
    public void Configure(EntityTypeBuilder<DailyAttendance> builder)
    {
        builder.ToTable("DailyAttendances", "attendance");
        builder.Property(x => x.Remarks).HasMaxLength(250);
        builder.Property(x => x.UpdatedByUserId).HasMaxLength(450);
        builder.HasIndex(x => new { x.StudentId, x.AttendanceDate }).IsUnique();
        builder.HasIndex(x => new { x.SectionId, x.AttendanceDate });
        builder.HasOne(x => x.Student)
            .WithMany()
            .HasForeignKey(x => x.StudentId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.AcademicYear)
            .WithMany()
            .HasForeignKey(x => x.AcademicYearId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Section)
            .WithMany()
            .HasForeignKey(x => x.SectionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class StudentLocalTemplateConfiguration : IEntityTypeConfiguration<StudentLocalTemplate>
{
    public void Configure(EntityTypeBuilder<StudentLocalTemplate> builder)
    {
        builder.ToTable("StudentLocalTemplates", "attendance");
        builder.Property(x => x.TemplateData).HasColumnType("nvarchar(max)").IsRequired();
        builder.Property(x => x.ExternalId).HasMaxLength(200).IsRequired();
        builder.HasIndex(x => new { x.StudentId, x.BiometricType }).IsUnique();
        builder.HasIndex(x => x.ExternalId);
        builder.HasOne(x => x.Student)
            .WithMany(x => x.LocalTemplates)
            .HasForeignKey(x => x.StudentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class SchoolHolidayConfiguration : IEntityTypeConfiguration<SchoolHoliday>
{
    public void Configure(EntityTypeBuilder<SchoolHoliday> builder)
    {
        builder.ToTable("SchoolHolidays", "attendance");
        builder.Property(x => x.Title).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(250);
        builder.HasIndex(x => new { x.SchoolId, x.HolidayDate })
            .IsUnique()
            .HasFilter("[RepeatsAnnually] = 0");
        builder.HasIndex(x => new { x.SchoolId, x.RecurringMonth, x.RecurringDay })
            .IsUnique()
            .HasFilter("[RepeatsAnnually] = 1");
        builder.HasOne(x => x.School)
            .WithMany()
            .HasForeignKey(x => x.SchoolId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class AttendanceNotificationLogConfiguration : IEntityTypeConfiguration<AttendanceNotificationLog>
{
    public void Configure(EntityTypeBuilder<AttendanceNotificationLog> builder)
    {
        builder.ToTable("AttendanceNotificationLogs", "attendance");
        builder.Property(x => x.RecipientPhone).HasMaxLength(30).IsRequired();
        builder.Property(x => x.Message).HasMaxLength(500).IsRequired();
        builder.HasIndex(x => new { x.SchoolId, x.AttendanceDate, x.StudentId, x.NotificationType }).IsUnique();
        builder.HasOne(x => x.Student)
            .WithMany()
            .HasForeignKey(x => x.StudentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class StaffDailyAttendanceConfiguration : IEntityTypeConfiguration<StaffDailyAttendance>
{
    public void Configure(EntityTypeBuilder<StaffDailyAttendance> builder)
    {
        builder.ToTable("StaffDailyAttendances", "attendance");
        builder.Property(x => x.Remarks).HasMaxLength(250);
        builder.Property(x => x.UpdatedByUserId).HasMaxLength(450);
        builder.HasIndex(x => new { x.TeacherId, x.AttendanceDate }).IsUnique();
        builder.HasIndex(x => new { x.SchoolId, x.AttendanceDate });
        builder.HasOne(x => x.Teacher)
            .WithMany()
            .HasForeignKey(x => x.TeacherId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class AppExceptionLogConfiguration : IEntityTypeConfiguration<AppExceptionLog>
{
    public void Configure(EntityTypeBuilder<AppExceptionLog> builder)
    {
        builder.ToTable("AppExceptionLogs", "shared");
        builder.Property(x => x.Source).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Message).HasMaxLength(2000).IsRequired();
        builder.Property(x => x.ExceptionType).HasMaxLength(300).IsRequired();
        builder.Property(x => x.InnerMessage).HasMaxLength(2000);
        builder.Property(x => x.StackTrace).HasMaxLength(8000);
        builder.Property(x => x.ConstraintName).HasMaxLength(200);
        builder.Property(x => x.UserId).HasMaxLength(450);
        builder.Property(x => x.UserEmail).HasMaxLength(256);
        builder.Property(x => x.ContextJson).HasMaxLength(4000);
        builder.HasIndex(x => x.CreatedAt);
    }
}

