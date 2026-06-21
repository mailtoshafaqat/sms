using SMS.Domain.Common;
using SMS.Domain.Entities.Attendance;
using SMS.Domain.Enums;
namespace SMS.Domain.Entities.Shared;

public class School : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? WhatsAppNumber { get; set; }
    public string? Email { get; set; }
    public string? LogoPath { get; set; }
    public TimeOnly SchoolStartTime { get; set; } = new(7, 30);
    public int LateAfterMinutes { get; set; } = 15;
    public TimeOnly SchoolEndTime { get; set; } = new(13, 0);
    public WeeklyOffDays WeeklyOffDays { get; set; } = WeeklyOffDays.Sunday;
    public bool NotifyAbsent { get; set; } = true;
    public bool NotifyLate { get; set; } = true;
    public string? AbsentNotificationTemplate { get; set; }
    public string? LateNotificationTemplate { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<AcademicYear> AcademicYears { get; set; } = [];
    public ICollection<ClassRoom> Classes { get; set; } = [];
    public ICollection<Student> Students { get; set; } = [];
    public ICollection<Teacher> Teachers { get; set; } = [];
    public ICollection<BiometricDevice> BiometricDevices { get; set; } = [];
}

public class AcademicYear : BaseEntity
{
    public int SchoolId { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public bool IsCurrent { get; set; }

    public School School { get; set; } = null!;
    public ICollection<StudentEnrollment> Enrollments { get; set; } = [];
}

public class ClassRoom : BaseEntity
{
    public int SchoolId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;

    public School School { get; set; } = null!;
    public ICollection<Section> Sections { get; set; } = [];
}

public class Section : BaseEntity
{
    public int ClassRoomId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public int? ClassTeacherId { get; set; }
    public bool IsActive { get; set; } = true;

    public ClassRoom ClassRoom { get; set; } = null!;
    public Teacher? ClassTeacher { get; set; }
    public ICollection<StudentEnrollment> Enrollments { get; set; } = [];
}

public class Student : BaseEntity
{
    public int SchoolId { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? FatherName { get; set; }
    public string? Phone { get; set; }
    public string? WhatsAppNumber { get; set; }
    public string? PhotoPath { get; set; }
    public bool IsActive { get; set; } = true;

    public string FullName => $"{FirstName} {LastName}".Trim();

    public School School { get; set; } = null!;
    public ICollection<StudentEnrollment> Enrollments { get; set; } = [];
    public ICollection<StudentBiometricMap> BiometricMaps { get; set; } = [];
    public ICollection<StudentLocalTemplate> LocalTemplates { get; set; } = [];
}

public class StudentEnrollment : BaseEntity
{
    public int StudentId { get; set; }
    public int AcademicYearId { get; set; }
    public int SectionId { get; set; }
    public string RollNumber { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    public Student Student { get; set; } = null!;
    public AcademicYear AcademicYear { get; set; } = null!;
    public Section Section { get; set; } = null!;
}

public class StudentPromotion : BaseEntity
{
    public int StudentId { get; set; }
    public int AcademicYearId { get; set; }
    public int FromSectionId { get; set; }
    public int ToSectionId { get; set; }
    public PromotionSource Source { get; set; } = PromotionSource.Manual;
    public string? Notes { get; set; }
    public string? PromotedByUserId { get; set; }
    public DateTime PromotedAt { get; set; } = DateTime.UtcNow;

    public Student Student { get; set; } = null!;
    public AcademicYear AcademicYear { get; set; } = null!;
    public Section FromSection { get; set; } = null!;
    public Section ToSection { get; set; } = null!;
}

public class Teacher : BaseEntity
{
    public int SchoolId { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? UserId { get; set; }
    public bool IsActive { get; set; } = true;

    public string FullName => $"{FirstName} {LastName}".Trim();

    public School School { get; set; } = null!;
}

