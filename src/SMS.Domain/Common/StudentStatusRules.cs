using SMS.Domain.Enums;

namespace SMS.Domain.Common;

public static class StudentStatusRules
{
    public static bool IsAttendanceEligible(StudentStatus status) => status == StudentStatus.Active;

    public static bool RequiresStatusNote(StudentStatus status) =>
        status is StudentStatus.Suspended or StudentStatus.Expelled;

    public static string GetDisplayName(StudentStatus status) => status switch
    {
        StudentStatus.Active => "Active",
        StudentStatus.Suspended => "Suspended",
        StudentStatus.Expelled => "Expelled",
        StudentStatus.Transferred => "Transferred",
        StudentStatus.Left => "Left",
        _ => status.ToString()
    };

    public static string GetStatusGuidance(StudentStatus status) => status switch
    {
        StudentStatus.Suspended =>
            "Temporary — student is out for a fixed period but may return. Hidden from attendance and gate until set back to Active.",
        StudentStatus.Expelled =>
            "Permanent removal from school — serious or repeated misconduct. A reason is required.",
        StudentStatus.Left =>
            "Student left on their own or family withdrew — not expelled. Record kept for history.",
        StudentStatus.Transferred =>
            "Moved to another school — transfer certificate issued. Not attending here anymore.",
        _ => string.Empty
    };

    public static string GetStatusNoteExample(StudentStatus status) => status switch
    {
        StudentStatus.Suspended => "e.g. Suspended 2 weeks for fighting — review on 15 Jul 2026",
        StudentStatus.Expelled => "e.g. Expelled by disciplinary committee — repeated cheating, 10 Jun 2026",
        StudentStatus.Left => "e.g. Family moved to Lahore — admission withdrawn, Mar 2026",
        StudentStatus.Transferred => "e.g. Transferred to City School — TC issued 5 May 2026",
        _ => "Reason or remarks..."
    };
}
