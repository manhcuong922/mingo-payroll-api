using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces.Attendance;

namespace MPS.Application.Features.Attendance.Common;

/// <summary>
/// Cross-check chống trùng ngày giữa 3 loại đơn Leave / WFH / LateEarly.
/// Chỉ chấp nhận 1 đơn (Pending hoặc Approved) trên 1 ngày; Rejected/Cancelled
/// không tính. Throw <see cref="DomainValidationException"/> với thông báo
/// nêu rõ loại đơn nào đang chặn.
/// </summary>
public sealed class AttendanceRequestDayConflictChecker
{
    private readonly ILeaveRequestRepository _leaveRepository;
    private readonly IWfhRequestRepository _wfhRepository;
    private readonly ILateEarlyRequestRepository _lateEarlyRepository;

    public AttendanceRequestDayConflictChecker(
        ILeaveRequestRepository leaveRepository,
        IWfhRequestRepository wfhRepository,
        ILateEarlyRequestRepository lateEarlyRepository)
    {
        _leaveRepository = leaveRepository;
        _wfhRepository = wfhRepository;
        _lateEarlyRepository = lateEarlyRepository;
    }

    /// <summary>
    /// Đảm bảo trong khoảng [startDate, endDate] employee KHÔNG có bất kỳ đơn
    /// Leave/WFH/LateEarly nào ở trạng thái Pending hoặc Approved.
    /// </summary>
    /// <param name="requestKind">Loại đơn đang tạo — dùng để loại trừ khi update.</param>
    /// <param name="excludeRequestId">Bỏ qua đơn hiện tại (khi update).</param>
    public async Task EnsureNoConflictAsync(
        int employeeInformationId,
        DateOnly startDate,
        DateOnly endDate,
        AttendanceRequestKind requestKind,
        int? excludeRequestId,
        CancellationToken cancellationToken = default)
    {
        var leaveExclude = requestKind == AttendanceRequestKind.Leave ? excludeRequestId : null;
        var wfhExclude = requestKind == AttendanceRequestKind.Wfh ? excludeRequestId : null;
        var lateEarlyExclude = requestKind == AttendanceRequestKind.LateEarly ? excludeRequestId : null;

        // Await tuần tự — 3 repository share cùng 1 AppDbContext scoped;
        // EF Core DbContext không thread-safe, chạy song song sẽ throw
        // "A second operation started on this context before a previous operation completed".
        var hasLeave = await _leaveRepository.HasPendingOrApprovedInRangeAsync(
            employeeInformationId, startDate, endDate, leaveExclude, cancellationToken);
        var hasWfh = await _wfhRepository.HasPendingOrApprovedInRangeAsync(
            employeeInformationId, startDate, endDate, wfhExclude, cancellationToken);
        var hasLateEarly = await _lateEarlyRepository.HasPendingOrApprovedInRangeAsync(
            employeeInformationId, startDate, endDate, lateEarlyExclude, cancellationToken);

        if (!hasLeave && !hasWfh && !hasLateEarly) return;

        var conflicts = new List<string>();
        if (hasLeave) conflicts.Add("nghỉ phép");
        if (hasWfh) conflicts.Add("WFH");
        if (hasLateEarly) conflicts.Add("đi trễ/về sớm");

        var rangeLabel = startDate == endDate
            ? $"ngày {startDate:dd/MM/yyyy}"
            : $"khoảng {startDate:dd/MM/yyyy} → {endDate:dd/MM/yyyy}";

        throw new DomainValidationException(
            $"Đã tồn tại đơn {string.Join(", ", conflicts)} (Pending/Approved) trong {rangeLabel}. "
            + "Mỗi ngày chỉ được có 1 đơn Leave/WFH/LateEarly — vui lòng hủy đơn cũ trước khi tạo đơn mới.");
    }
}

public enum AttendanceRequestKind
{
    Leave = 1,
    Wfh = 2,
    LateEarly = 3,
}
