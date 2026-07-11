using AttendanceEnums = MPS.Domain.Enums.Attendance;

namespace MPS.Domain.Entities.Attendance;

/// <summary>
/// Đơn đăng ký làm việc từ xa (WFH). Tính là 1 ngày công hưởng nguyên lương —
/// chỉ khác về địa điểm vật lý. Cần WorkPlan chi tiết để manager nghiệm thu.
/// </summary>
public class WfhRequest
{
    public int Id { get; set; }

    public int EmployeeInformationId { get; set; }
    public EmployeeInformation EmployeeInformation { get; set; } = null!;

    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public decimal TotalDays { get; set; }

    /// <summary>Bắt buộc — chi tiết task/WBS sẽ hoàn thành trong quãng WFH.</summary>
    public string WorkPlan { get; set; } = string.Empty;

    public AttendanceEnums.OvertimeApproverRoute? ApproverRoute { get; set; }
    public AttendanceEnums.WfhRequestStatus Status { get; set; }

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
