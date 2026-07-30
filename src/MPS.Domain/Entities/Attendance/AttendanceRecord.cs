using AttendanceEnums = MPS.Domain.Enums.Attendance;

namespace MPS.Domain.Entities.Attendance;

public class AttendanceRecord
{
    public int Id { get; set; }

    public int EmployeeInformationId { get; set; }
    public EmployeeInformation EmployeeInformation { get; set; } = null!;

    public DateOnly WorkDate { get; set; }

    /// <summary>
    /// FK tới ShiftTemplate — NULL khi record này chấm cho OT (dùng OvertimeRequestId).
    /// Invariant: exactly-one-of (ShiftTemplateId, OvertimeRequestId) phải non-null.
    /// </summary>
    public int? ShiftTemplateId { get; set; }
    public ShiftTemplate? ShiftTemplate { get; set; }

    /// <summary>
    /// FK tới OvertimeRequest — set khi HR chấm công cho OT đã được duyệt.
    /// Payroll composer join theo field này để tra multiplier từ slot khớp giờ.
    /// NULL cho record ca chính (dùng ShiftTemplateId).
    /// </summary>
    public int? OvertimeRequestId { get; set; }
    public OvertimeRequest? OvertimeRequest { get; set; }

    /// <summary>
    /// Case 2 — LIÊN KẾT tới đơn đi muộn/về sớm đã trừ giờ vào bản ghi CA CHÍNH này. Marker (không FK
    /// cứng) để: (1) truy vết ai làm giảm giờ công của ngày, (2) idempotent — không trừ hai lần cùng
    /// một đơn nếu duyệt/áp lại. NULL khi bản ghi chưa bị đơn đi muộn/về sớm nào tác động.
    /// </summary>
    public int? LateEarlyRequestId { get; set; }

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
