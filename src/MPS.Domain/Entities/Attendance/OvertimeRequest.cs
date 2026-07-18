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
    /// Route duyệt cuối do PM chọn khi Forward — HR hoặc DepartmentManager.
    /// Null khi Status=Pending (chưa PM review) hoặc Status=Rejected-by-PM.
    /// Handler ApproverDecide dispatch theo field này.
    /// </summary>
    public AttendanceEnums.OvertimeApproverRoute? ApproverRoute { get; set; }

    /// <summary>
    /// [DEPRECATED] Nhóm N request cùng batch trong luồng v1 (PM tạo batch cho N NV).
    /// Luồng v3 mới NV tự đăng ký từng record → field này luôn null cho record mới.
    /// Giữ cột DB để không phá query cũ và audit lịch sử.
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

    // --- Audit tầng 1 — PM (tổ trưởng đơn) review ---
    /// <summary>UserId của PM (Project.ResponsibleUserId) đã Forward hoặc Reject.</summary>
    public int? PmReviewedByUserId { get; set; }
    public User? PmReviewedByUser { get; set; }
    public DateTime? PmReviewedAt { get; set; }
    public string? PmReviewNote { get; set; }

    // --- Audit tầng 2 — Approver (HR hoặc DM tuỳ ApproverRoute) decide ---
    /// <summary>UserId của Approver (HR hoặc DM) đã Approve hoặc Reject cuối.</summary>
    public int? ApproverReviewedByUserId { get; set; }
    public User? ApproverReviewedByUser { get; set; }
    public DateTime? ApproverReviewedAt { get; set; }
    public string? ApproverReviewNote { get; set; }

    // --- Legacy mirror — set = giá trị của tầng quyết định cuối ---
    /// <summary>
    /// Mirror của người ra quyết định cuối: nếu Approved → = ApproverReviewedByUserId;
    /// nếu Rejected → = PmReviewedByUserId (PM reject) hoặc ApproverReviewedByUserId
    /// (Approver reject). Payroll và các query cũ đọc field này, không phá.
    /// </summary>
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