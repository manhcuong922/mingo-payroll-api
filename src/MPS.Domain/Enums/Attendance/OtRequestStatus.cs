namespace MPS.Domain.Enums.Attendance;

public enum OtRequestStatus
{
    Pending = 1,
    Approved = 2,
    Rejected = 3,
    /// <summary>
    /// PM (người tạo đơn) tự huỷ khi đơn còn Pending. Khác Rejected — Rejected là
    /// approver từ chối. Đơn Cancelled không đi tiếp vào payroll và không thể revive.
    /// </summary>
    Cancelled = 4
}