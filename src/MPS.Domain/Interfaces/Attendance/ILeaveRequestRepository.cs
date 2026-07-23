using MPS.Domain.Entities.Attendance;
using MPS.Domain.Enums.Attendance;

namespace MPS.Domain.Interfaces.Attendance;

public interface ILeaveRequestRepository
{
    Task<(IReadOnlyList<LeaveRequest> Items, int Total)> GetPagedAsync(
        int? employeeInformationId,
        LeaveRequestStatus? status,
        DateOnly? fromDate,
        DateOnly? toDate,
        int page,
        int size,
        int? departmentIdFilter = null,
        OvertimeApproverRoute? approverRouteFilter = null,
        CancellationToken cancellationToken = default);

    Task<LeaveRequest?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EmployeeLeaveMonthlySummary>> GetYearlyEmployeeSummaryAsync(
        int year,
        CancellationToken cancellationToken = default);

    Task<int> AddAsync(LeaveRequest entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lưu nhiều đơn nghỉ trong MỘT lượt ghi.
    /// Dùng cho `[D2]`: đơn xin vượt quỹ được tách thành phần CÓ LƯƠNG (dùng nốt quỹ) và phần
    /// KHÔNG LƯƠNG. Hai phần phải cùng sống hoặc cùng chết — nếu chỉ phần có lương được lưu thì
    /// người lao động mất số ngày nghỉ đã xin, còn nếu chỉ phần không lương được lưu thì họ mất
    /// quỹ phép. Trả về id theo đúng thứ tự truyền vào.
    /// </summary>
    Task<IReadOnlyList<int>> AddRangeAsync(
        IReadOnlyList<LeaveRequest> entities,
        CancellationToken cancellationToken = default);

    Task<bool> UpdateAsync(LeaveRequest entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// FT-13: atomically approve a pending leave request.
    /// Performs a transactional operation that:
    /// 1. Re-checks remaining balance for leave types where LeaveType.DeductsBalance = true (race-safe under read-committed).
    /// 2. Increments LeaveBalance.UsedDays when applicable.
    /// 3. Upserts an AttendanceRecord with Status=OnLeave for each day in [StartDate, EndDate].
    /// 4. Marks the request as Approved + audit (reviewer, reviewedAt, reviewNote).
    /// </summary>
    /// <returns>true if approved; false if the request is missing.</returns>
    Task<bool> ApproveAndApplyAsync(
        int requestId,
        int reviewedByUserId,
        string? reviewNote,
        DateTime nowUtc,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancel a request. If it is Approved, transactionally reverts LeaveBalance.UsedDays
    /// and removes auto-created AttendanceRecords for the range [StartDate, EndDate].
    /// Only the creator may cancel; a Pending request may be cancelled unconditionally,
    /// an Approved one only if StartDate is strictly in the future.
    /// </summary>
    /// <returns>true if cancelled; false if the request is missing.</returns>
    Task<bool> CancelAsync(
        int requestId,
        int cancelledByUserId,
        string? cancelReason,
        DateTime nowUtc,
        CancellationToken cancellationToken = default);
}

public sealed record EmployeeLeaveMonthlySummary(
    int EmployeeInformationId,
    int Month,
    decimal ApprovedDays,
    int ApprovedRequestCount,
    decimal PendingDays,
    int PendingRequestCount);
