using AttendanceEnums = MPS.Domain.Enums.Attendance;

namespace MPS.Domain.Entities.Attendance;

public class LeaveRequest
{
    public int Id { get; set; }

    public int EmployeeInformationId { get; set; }
    public EmployeeInformation EmployeeInformation { get; set; } = null!;

    public int LeaveTypeId { get; set; }
    public LeaveType LeaveType { get; set; } = null!;

    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public decimal TotalDays { get; set; }

    /// <summary>
    /// Snapshot IsPaid tại thời điểm tạo (BR-17). Handler tự gán từ LeaveType active,
    /// không nhận từ client — Payroll Engine đọc trực tiếp để tính lương.
    /// </summary>
    public bool IsPaid { get; set; }

    public AttendanceEnums.LeaveRequestStatus Status { get; set; }

    public string? Reason { get; set; }

    /// <summary>
    /// Bắt buộc khi LeaveType.RequireDocument = true.
    /// </summary>
    public string? SupportingDocumentUrl { get; set; }

    public string? ReviewNote { get; set; }

    public int? ReviewedByUserId { get; set; }
    public User? ReviewedByUser { get; set; }
    public DateTime? ReviewedAt { get; set; }

    public DateTime CreatedAt { get; set; }
    public int CreatedByUserId { get; set; }
    public User CreatedByUser { get; set; } = null!;
    public DateTime? UpdatedAt { get; set; }
    public int? UpdatedByUserId { get; set; }
    public User? UpdatedByUser { get; set; }
}
