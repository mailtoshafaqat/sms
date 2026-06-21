namespace SMS.Domain.Enums;

public enum AttendanceStatus
{
    Present = 1,
    Absent = 2,
    Late = 3,
    Leave = 4,
    Holiday = 5
}

public enum ScanDirection
{
    In = 1,
    Out = 2
}

public enum BiometricConnectionType
{
    Usb = 1,
    Tcp = 2
}

public enum BiometricType
{
    Fingerprint = 1,
    Face = 2,
    Card = 3,
    Both = 4
}

public enum UserRole
{
    Admin = 1,
    Teacher = 2
}

public enum PromotionSource
{
    Manual = 1,
    ExamPassed = 2,
    BulkEndOfYear = 3
}

