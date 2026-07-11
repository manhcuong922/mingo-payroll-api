namespace MPS.Domain.Enums.Notifications;

/// <summary>
/// Loại thông báo (mã ổn định, dùng để FE map icon/định tuyến nếu cần).
/// Thêm loại mới = thêm 1 giá trị ở đây + 1 template tương ứng ở Application/Features/Notifications/Templates.
/// KHÔNG đổi giá trị số của các mục đã phát hành để tránh vỡ dữ liệu cũ trong DB.
/// </summary>
public enum NotificationType
{
    /// <summary>Thông báo hệ thống chung, không gắn nghiệp vụ cụ thể.</summary>
    System = 0,

    /// <summary>Đơn hàng/dự án được chuyển sang Completed.</summary>
    ProjectCompleted = 1,

    /// <summary>Người dùng được giao một công việc (task).</summary>
    TaskAssigned = 2,

    /// <summary>Có đơn nghỉ phép mới cần duyệt.</summary>
    LeaveRequestSubmitted = 3,

    /// <summary>Đơn nghỉ phép đã được duyệt/từ chối.</summary>
    LeaveRequestDecided = 4,

    /// <summary>Hợp đồng sắp hết hạn.</summary>
    ContractExpiringSoon = 5,

    /// <summary>Manager tạo ca làm việc → nhân viên trong ca nhận.</summary>
    ShiftCreated = 6,

    /// <summary>Tạo nhiệm vụ/công việc mới → người liên quan nhận.</summary>
    TaskCreated = 7,

    /// <summary>Tạo bảng lương → nhân viên nhận.</summary>
    PayrollCreated = 8,
}
