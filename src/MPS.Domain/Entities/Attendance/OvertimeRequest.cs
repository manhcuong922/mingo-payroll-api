using AttendanceEnums = MPS.Domain.Enums.Attendance;

namespace MPS.Domain.Entities.Attendance;

public class OvertimeRequest
{
    public int Id { get; set; }

    public int EmployeeInformationId { get; set; }
    public EmployeeInformation EmployeeInformation { get; set; } = null!;

    public DateOnly WorkDate { get; set; }
    public TimeOnly OtStartTime { get; set; }
    public TimeOnly OtEndTime { get; set; }
    public decimal OtHours { get; set; }

    /// <summary>
    /// FK tới slot template do HR/Manager tạo. Required khi tạo mới (snapshot
    /// OtStartTime/OtEndTime từ slot); nullable để legacy data trước khi land
    /// module slot không vỡ.
    /// </summary>
    public int? OvertimeSlotTemplateId { get; set; }
    public OvertimeSlotTemplate? OvertimeSlotTemplate { get; set; }

    /// <summary>
    /// Phase B: giờ OT rơi NGOÀI khung đêm. Compute qua <c>IWorkHourSplitter</c>
    /// khi Create + recompute khi Approve. Composer Phase C dùng để áp uplift
    /// Điều 98.1.a (OT ngày thường + 50%).
    /// </summary>
    public decimal OtDayHours { get; set; }

    /// <summary>
    /// Phase B: giờ OT rơi TRONG khung đêm. Composer Phase C áp Điều 98.3 (OT
    /// đêm extra +20%) trên giờ này.
    /// </summary>
    public decimal OtNightHours { get; set; }

    public AttendanceEnums.OtRequestStatus Status { get; set; }

    public string? Reason { get; set; }
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