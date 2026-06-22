namespace MPS.Domain.Entities.Attendance;

public class EmployeeShiftAssignment
{
    public int Id { get; set; }

    public int EmployeeInformationId { get; set; }
    public EmployeeInformation EmployeeInformation { get; set; } = null!;

    public int ShiftTemplateId { get; set; }
    public ShiftTemplate ShiftTemplate { get; set; } = null!;

    public DateOnly WorkDate { get; set; }

    /// <summary>
    /// Nullable FK back to <c>ScheduleTemplate</c> that owns this assignment.
    /// Null = manual/orphan (not created by any template). Set when a template's
    /// Apply produces this row, or when a different template's Apply takes over
    /// via <c>ReplaceIfNoAttendance</c>.
    /// </summary>
    public int? ScheduleTemplateId { get; set; }
    public ScheduleTemplate? ScheduleTemplate { get; set; }

    public DateTime CreatedAt { get; set; }
    public int CreatedByUserId { get; set; }
    public User CreatedByUser { get; set; } = null!;
}