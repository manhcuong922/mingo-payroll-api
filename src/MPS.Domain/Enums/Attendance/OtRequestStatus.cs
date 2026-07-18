namespace MPS.Domain.Enums.Attendance;

public enum OtRequestStatus
{
    /// <summary>
    /// NV vừa submit — chờ PM (tổ trưởng đơn = Project.ResponsibleUserId) review.
    /// Mang nghĩa "PendingPm" trong luồng 2-tầng mới.
    /// </summary>
    Pending = 1,

    /// <summary>Final — Approver (HR hoặc DM tuỳ ApproverRoute) đã duyệt.</summary>
    Approved = 2,

    /// <summary>Final — PM hoặc Approver từ chối.</summary>
    Rejected = 3,

    /// <summary>
    /// NV tự huỷ đơn khi còn Pending. Khác Rejected — Rejected là quản lý từ chối.
    /// Đơn Cancelled không đi vào payroll và không revive được.
    /// </summary>
    Cancelled = 4,

    /// <summary>
    /// PM đã Forward — chờ Approver (HR hoặc DM, đọc ApproverRoute để biết inbox nào)
    /// duyệt cuối. Đây là stage giữa Pending và Approved/Rejected.
    /// </summary>
    PendingApprover = 5
}