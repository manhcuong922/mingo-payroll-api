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
        CancellationToken cancellationToken = default);

    Task<LeaveRequest?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<int> AddAsync(LeaveRequest entity, CancellationToken cancellationToken = default);

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
}
