using MPS.Domain.Entities.Compliance;
using AttendanceEnums = MPS.Domain.Enums.Attendance;

namespace MPS.Domain.Entities.Attendance;

public class OvertimeRequest
{
    public int Id { get; set; }

    public int EmployeeInformationId { get; set; }
    public EmployeeInformation EmployeeInformation { get; set; } = null!;

    /// <summary>
    /// FK tới Project (đơn hàng SX). Nullable cho legacy records (self-created cũ)
    /// không vỡ; NEW records luôn required (batch handler enforce).
    /// </summary>
    public int? ProjectId { get; set; }
    public Project? Project { get; set; }

    /// <summary>
    /// PM chọn route duyệt khi tạo — HR hoặc DepartmentManager. Handler duyệt
    /// gate bằng field này. Nullable cho legacy records.
    /// </summary>
    public AttendanceEnums.OvertimeApproverRoute? ApproverRoute { get; set; }

    /// <summary>
    /// Nhóm N request cùng batch. PM tạo cho 5 NV → 5 record cùng BatchId để
    /// UI group hiển thị "1 batch của PM X — 5 NV, project Y".
    /// </summary>
    public Guid? BatchId { get; set; }

    public DateOnly WorkDate { get; set; }
    public TimeOnly OtStartTime { get; set; }
    public TimeOnly OtEndTime { get; set; }
    public decimal OtHours { get; set; }

    /// <summary>
    /// FK snapshot version LaborLawConstants tại thời điểm tạo request. Payroll composer
    /// dùng version này để compute multiplier từ công thức — không đọc version active hiện
    /// tại. Đảm bảo rate không bị ảnh hưởng khi HR đổi luật sau khi request được tạo.
    /// Nullable cho legacy record trước khi land refactor này.
    /// </summary>
    public int? LaborLawConstantsVersionId { get; set; }
    public LaborLawConstantsVersion? LaborLawConstantsVersion { get; set; }

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