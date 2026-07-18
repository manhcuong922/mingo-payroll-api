namespace MPS.Domain.Entities.Attendance;

public class ShiftTemplate
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public TimeOnly? BreakStartTime { get; set; }
    public TimeOnly? BreakEndTime { get; set; }
    public decimal StandardHours { get; set; }
    public decimal StandardDays { get; set; } = 1.00m;

    /// <summary>
    /// FK NOT NULL tới <see cref="AttendanceRule"/> — single source of truth cho hệ số công.
    /// ShiftTemplate KHÔNG snapshot rate; mọi consumer đọc qua navigation property.
    /// </summary>
    public int AppliedAttendanceRuleId { get; set; }
    public AttendanceRule AppliedAttendanceRule { get; set; } = null!;

    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; }
    public int CreatedByUserId { get; set; }
    public User CreatedByUser { get; set; } = null!;
    public DateTime? UpdatedAt { get; set; }
    public int? UpdatedByUserId { get; set; }
    public User? UpdatedByUser { get; set; }

    public ICollection<EmployeeShiftAssignment> EmployeeShiftAssignments { get; set; } = new List<EmployeeShiftAssignment>();
    public ICollection<AttendanceRecord> AttendanceRecords { get; set; } = new List<AttendanceRecord>();
}
