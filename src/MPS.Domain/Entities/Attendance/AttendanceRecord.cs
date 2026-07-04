using AttendanceEnums = MPS.Domain.Enums.Attendance;

namespace MPS.Domain.Entities.Attendance;

public class AttendanceRecord
{
    public int Id { get; set; }

    public int EmployeeInformationId { get; set; }
    public EmployeeInformation EmployeeInformation { get; set; } = null!;

    public DateOnly WorkDate { get; set; }

    public int ShiftTemplateId { get; set; }
    public ShiftTemplate ShiftTemplate { get; set; } = null!;

    public DateTime? CheckInAt { get; set; }
    public DateTime? CheckOutAt { get; set; }

    /// <summary>
    /// Giờ làm rơi NGOÀI khung đêm (22h–6h). Compute từ CheckIn/CheckOut và
    /// shift break qua <c>IWorkHourSplitter</c> mỗi khi record được tạo/update.
    /// 0 khi <see cref="Status"/> = OnLeave hoặc Absent.
    /// </summary>
    public decimal DayHours { get; set; }

    /// <summary>
    /// Giờ làm rơi TRONG khung đêm (22h–6h). Theo Điều 98.2 BLLĐ — payroll áp
    /// hệ số +30% trên giờ này.
    /// </summary>
    public decimal NightHours { get; set; }

    public AttendanceEnums.AttendanceStatus Status { get; set; }

    public string? Notes { get; set; }
    public string? OverrideReason { get; set; }

    public DateTime CreatedAt { get; set; }
    public int CreatedByUserId { get; set; }
    public User CreatedByUser { get; set; } = null!;
    public DateTime? UpdatedAt { get; set; }
    public int? UpdatedByUserId { get; set; }
    public User? UpdatedByUser { get; set; }

    public ICollection<AttendanceAdjustment> AttendanceAdjustments { get; set; } = new List<AttendanceAdjustment>();
}
