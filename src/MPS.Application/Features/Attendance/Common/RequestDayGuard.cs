using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces.Attendance;

namespace MPS.Application.Features.Attendance.Common;

/// <summary>
/// [D8] — chặn đơn chồng ngày CHÉO LOẠI. Trước bản vá, mỗi loại đơn tự kiểm lấy một phần
/// (WFH↔WFH, WFH↔Nghỉ, LateEarly cùng kind) nên vẫn tạo được: Nghỉ↔Nghỉ, Nghỉ↔WFH, Nghỉ↔LateEarly,
/// LateEarly↔WFH… ⇒ 1 ngày có nhiều đơn mâu thuẫn, sinh sai công và sai lương.
/// </summary>
public static class RequestDayGuard
{
    public static async Task EnsureDayFreeAsync(
        IRequestDayOccupancyReader reader,
        int employeeInformationId,
        DateOnly start,
        DateOnly end,
        RequestDayKind? excludeKind = null,
        int? excludeRequestId = null,
        CancellationToken cancellationToken = default)
    {
        var occupied = await reader.FindFirstOccupiedAsync(
            employeeInformationId, start, end, excludeKind, excludeRequestId, cancellationToken);

        if (occupied is null) return;

        throw new DomainValidationException(
            $"Đã tồn tại đơn {Describe(occupied.Kind)} (Pending/Approved) trong ngày {occupied.Date:dd/MM/yyyy}. " +
            "Mỗi ngày chỉ được có 1 đơn Leave/WFH/LateEarly — vui lòng hủy đơn cũ trước khi tạo đơn mới.");
    }

    /// <summary>
    /// OT ngược lại chỉ loại trừ với NGHỈ PHÉP đã duyệt: hôm đó nhân viên không đi làm nên không thể
    /// có giờ làm thêm. WFH / đi muộn - về sớm vẫn được đăng ký OT bình thường.
    /// </summary>
    public static async Task EnsureNotOnApprovedLeaveAsync(
        IRequestDayOccupancyReader reader,
        int employeeInformationId,
        DateOnly start,
        DateOnly end,
        CancellationToken cancellationToken = default)
    {
        var leaveDay = await reader.FindFirstApprovedLeaveDayAsync(
            employeeInformationId, start, end, cancellationToken);

        if (leaveDay is null) return;

        throw new DomainValidationException(
            $"Ngày {leaveDay.Value:dd/MM/yyyy} nhân viên đang nghỉ phép (đơn đã duyệt) — không thể đăng ký làm thêm giờ.");
    }

    private static string Describe(RequestDayKind kind) => kind switch
    {
        RequestDayKind.Leave => "nghỉ phép",
        RequestDayKind.Wfh => "WFH",
        RequestDayKind.LateEarly => "đi muộn/về sớm",
        _ => kind.ToString(),
    };
}
