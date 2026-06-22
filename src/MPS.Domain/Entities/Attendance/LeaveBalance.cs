namespace MPS.Domain.Entities.Attendance;

public class LeaveBalance
{
    public int Id { get; set; }

    public int EmployeeInformationId { get; set; }
    public EmployeeInformation EmployeeInformation { get; set; } = null!;

    public int Year { get; set; }

    public int LeaveTypeId { get; set; }
    public LeaveType LeaveType { get; set; } = null!;

    /// <summary>BR-15 — Điều 113: 12 ngày cơ bản/năm.</summary>
    public decimal BaseDays { get; set; }

    /// <summary>BR-15 — Điều 114: thêm 1 ngày / 5 năm thâm niên.</summary>
    public decimal SeniorityBonusDays { get; set; }

    /// <summary>= BaseDays + SeniorityBonusDays (handler tự tính khi init/recompute).</summary>
    public decimal TotalDays { get; set; }
    public decimal UsedDays { get; set; }

    public DateTime CreatedAt { get; set; }
    public int CreatedByUserId { get; set; }
    public User CreatedByUser { get; set; } = null!;
    public DateTime? UpdatedAt { get; set; }
    public int? UpdatedByUserId { get; set; }
    public User? UpdatedByUser { get; set; }
}
