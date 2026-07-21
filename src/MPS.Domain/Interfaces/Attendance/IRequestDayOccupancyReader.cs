namespace MPS.Domain.Interfaces.Attendance;

/// <summary>
/// Loại đơn từ chấm công chiếm dụng một NGÀY của nhân viên.
/// </summary>
public enum RequestDayKind
{
    Leave = 1,
    Wfh = 2,
    LateEarly = 3,
}

/// <summary>Ngày đã bị 1 đơn khác chiếm — dùng để dựng thông báo lỗi.</summary>
public sealed record OccupiedDay(DateOnly Date, RequestDayKind Kind);

/// <summary>
/// [D8] — mỗi ngày làm việc chỉ được tồn tại 1 đơn Leave/WFH/LateEarly còn hiệu lực
/// (Pending hoặc Approved) cho cùng một nhân viên. Trước bản vá này guard chỉ tồn tại lẻ tẻ
/// (WFH↔WFH, WFH↔Nghỉ, LateEarly cùng kind) nên vẫn tạo được đơn chồng chéo ⇒ sai công, sai lương.
/// </summary>
public interface IRequestDayOccupancyReader
{
    /// <summary>
    /// Tìm ngày đầu tiên trong [start, end] đã có đơn còn hiệu lực. Trả null nếu khoảng ngày còn trống.
    /// <paramref name="excludeKind"/> + <paramref name="excludeRequestId"/> để bỏ qua chính đơn đang sửa.
    /// </summary>
    Task<OccupiedDay?> FindFirstOccupiedAsync(
        int employeeInformationId,
        DateOnly start,
        DateOnly end,
        RequestDayKind? excludeKind = null,
        int? excludeRequestId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Ngày nhân viên đang nghỉ phép ĐÃ DUYỆT trong [start, end]. Dùng riêng cho OT: nghỉ phép và OT
    /// loại trừ nhau, còn WFH/đi muộn thì vẫn có thể làm thêm giờ trong ngày.
    /// </summary>
    Task<DateOnly?> FindFirstApprovedLeaveDayAsync(
        int employeeInformationId,
        DateOnly start,
        DateOnly end,
        CancellationToken cancellationToken = default);
}
