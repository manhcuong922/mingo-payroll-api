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
    public decimal WorkRateNormal { get; set; } = 1.00m;
    public decimal WorkRateWeeklyRest { get; set; } = 2.00m;
    public decimal WorkRateHoliday { get; set; } = 3.00m;
    public bool IsNightShift { get; set; }
    public int? AppliedAttendanceRuleId { get; set; }
    public AttendanceRule? AppliedAttendanceRule { get; set; }
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
