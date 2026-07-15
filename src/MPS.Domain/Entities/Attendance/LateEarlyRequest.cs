using AttendanceEnums = MPS.Domain.Enums.Attendance;

namespace MPS.Domain.Entities.Attendance;

/// <summary>
/// Đơn xin đi trễ / về sớm. Approve route HR|DepartmentManager giống OT.
/// Không có batch — nhân viên tự nộp cho chính mình.
/// </summary>
public class LateEarlyRequest
{
    public int Id { get; set; }

    public int EmployeeInformationId { get; set; }
    public EmployeeInformation EmployeeInformation { get; set; } = null!;

    public DateOnly WorkDate { get; set; }
    public AttendanceEnums.LateEarlyKind Kind { get; set; }

    /// <summary>Số phút trễ/sớm — 1..240.</summary>
    public short Minutes { get; set; }

    /// <summary>Set khi WorkDate ≤ today (nộp trong/sau ngày). Đột xuất.</summary>
    public bool IsUnplanned { get; set; }

    /// <summary>
    /// True = approver quyết định trừ vào quỹ phép năm (khi vượt quota nội quy).
    /// False = tính vào lỗi chấm công (payroll module xử lý).
    /// </summary>
    public bool DeductFromLeaveBalance { get; set; }

    public AttendanceEnums.OvertimeApproverRoute? ApproverRoute { get; set; }
    public AttendanceEnums.LateEarlyRequestStatus Status { get; set; }

    public string? Reason { get; set; }
    public string? ReviewNote { get; set; }

    public int? ReviewedByUserId { get; set; }
    public User? ReviewedByUser { get; set; }
    public DateTime? ReviewedAt { get; set; }

    public DateTime? CancelledAt { get; set; }
    public int? CancelledByUserId { get; set; }
    public User? CancelledByUser { get; set; }
    public string? CancelReason { get; set; }

    public DateTime CreatedAt { get; set; }
    public int CreatedByUserId { get; set; }
    public User CreatedByUser { get; set; } = null!;
    public DateTime? UpdatedAt { get; set; }
    public int? UpdatedByUserId { get; set; }
    public User? UpdatedByUser { get; set; }
}
