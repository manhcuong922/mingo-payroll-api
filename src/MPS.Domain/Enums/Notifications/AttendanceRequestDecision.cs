namespace MPS.Domain.Enums.Notifications;

/// <summary>Kết quả duyệt đơn — dùng ở template <c>AttendanceRequestDecidedTemplate</c> (Phase 3).</summary>
public enum AttendanceRequestDecision : byte
{
    Approved = 1,
    Rejected = 2,
}
