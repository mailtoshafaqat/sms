using System.ComponentModel.DataAnnotations.Schema;
using SMS.Domain.Common;
using SMS.Domain.Entities.Shared;
using SMS.Domain.Enums;

namespace SMS.Domain.Entities.Attendance;

public class BiometricDevice : BaseEntity
{
    public int SchoolId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;

    [Column("Modality")]
    public BiometricType BiometricType { get; set; } = BiometricType.Fingerprint;

    public BiometricConnectionType ConnectionType { get; set; } = BiometricConnectionType.Usb;
    public string? IpAddress { get; set; }
    public int Port { get; set; } = 4370;
    public int MachineNumber { get; set; } = 1;
    public string? ComPort { get; set; }
    public int DuplicateScanBlockSeconds { get; set; } = 30;
    public bool IsConnected { get; set; }
    public DateTime? LastConnectedAt { get; set; }
    public DateTime? LastScanAt { get; set; }

    public School School { get; set; } = null!;
    public ICollection<StudentBiometricMap> BiometricMaps { get; set; } = [];
    public ICollection<AttendanceLog> AttendanceLogs { get; set; } = [];
}

public class StudentBiometricMap : BaseEntity
{
    public int StudentId { get; set; }
    public int BiometricDeviceId { get; set; }
    public string BiometricUserId { get; set; } = string.Empty;

    [Column("Modality")]
    public BiometricType BiometricType { get; set; } = BiometricType.Fingerprint;

    public Student Student { get; set; } = null!;
    public BiometricDevice BiometricDevice { get; set; } = null!;
}

public class AttendanceLog : BaseEntity
{
    public int SchoolId { get; set; }
    public int StudentId { get; set; }
    public int? BiometricDeviceId { get; set; }
    public DateOnly AttendanceDate { get; set; }
    public DateTime ScanTime { get; set; }
    public ScanDirection Direction { get; set; }

    [Column("ScanModality")]
    public BiometricType ScanType { get; set; } = BiometricType.Fingerprint;

    public string? Source { get; set; }

    public Student Student { get; set; } = null!;
    public BiometricDevice? BiometricDevice { get; set; }
}

public class DailyAttendance : BaseEntity
{
    public int SchoolId { get; set; }
    public int StudentId { get; set; }
    public int AcademicYearId { get; set; }
    public int SectionId { get; set; }
    public DateOnly AttendanceDate { get; set; }
    public AttendanceStatus Status { get; set; }
    public DateTime? CheckInTime { get; set; }
    public DateTime? CheckOutTime { get; set; }
    public string? Remarks { get; set; }
    public bool IsManualEntry { get; set; }
    public string? UpdatedByUserId { get; set; }

    public Student Student { get; set; } = null!;
    public AcademicYear AcademicYear { get; set; } = null!;
    public Section Section { get; set; } = null!;
}

public class StudentLocalTemplate : BaseEntity
{
    public int StudentId { get; set; }
    public BiometricType BiometricType { get; set; }
    public string TemplateData { get; set; } = string.Empty;
    public string ExternalId { get; set; } = string.Empty;

    public Student Student { get; set; } = null!;
}

public class SchoolHoliday : BaseEntity
{
    public int SchoolId { get; set; }
    public DateOnly HolidayDate { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool RepeatsAnnually { get; set; }
    public int RecurringMonth { get; set; }
    public int RecurringDay { get; set; }

    public School School { get; set; } = null!;
}

