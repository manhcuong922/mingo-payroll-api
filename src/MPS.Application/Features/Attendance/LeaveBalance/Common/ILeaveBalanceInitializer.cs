namespace MPS.Application.Features.Attendance.LeaveBalance.Common;

/// <summary>
/// #34/[D22] — tự khởi tạo quỹ phép năm (ANNUAL) cho NLĐ. Gọi bởi hook khi HĐ chuyển Active (NV mới
/// có ngay quỹ phép, không bị chặn cứng lúc duyệt đơn muộn/sớm) và bởi job cấp phép đầu năm.
/// </summary>
public interface ILeaveBalanceInitializer
{
    /// <summary>
    /// Đảm bảo NLĐ có bản ghi quỹ ANNUAL của năm <paramref name="year"/> — tạo nếu chưa có
    /// (idempotent: đã có ⇒ giữ nguyên UsedDays, trả false). Trả true nếu vừa tạo mới.
    /// </summary>
    Task<bool> EnsureAnnualBalanceAsync(
        int employeeInformationId, int year, int actorUserId, CancellationToken cancellationToken = default);
}
